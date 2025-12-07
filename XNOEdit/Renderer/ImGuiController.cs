using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using SDL3;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Builders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    public unsafe class ImGuiController : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly WgpuDevice _device;
        private readonly Queue* _queue;
        private readonly IntPtr _window;
        private readonly uint _framesInFlight;

        private ShaderModule* _shaderModule;
        private Sampler* _fontSampler;

        private BindGroupLayout* _commonBindGroupLayout;
        private BindGroupLayout* _imageBindGroupLayout;
        private RenderPipeline* _renderPipeline;

        private BindGroup* _commonBindGroup;

        private WgpuBuffer<Uniforms> _uniformsBuffer;

        private WindowRenderBuffers _windowRenderBuffers;

        private readonly Dictionary<nint, IntPtr> _textureBindGroups = [];
        private readonly Dictionary<nint, IntPtr> _gpuTextures = [];

        public ImGuiController(
            WebGPU wgpu,
            WgpuDevice device,
            IntPtr window,
            uint framesInFlight)
        {
            _wgpu = wgpu;
            _device = device;
            _window = window;
            _framesInFlight = framesInFlight;
            _queue = _wgpu.DeviceGetQueue(_device);

            Init();
        }

        public void Update(float delta)
        {
            SetPerFrameImGuiData(delta);
            ImGui.NewFrame();
        }

        public void Render(RenderPassEncoder* encoder)
        {
            ImGui.Render();
            DrawImGui(encoder);
        }

        public void BindImGuiTextureView(TextureView* view)
        {
            var id = (nint)view;

            if (_textureBindGroups.TryGetValue(id, out var existing))
            {
                _wgpu.BindGroupRelease((BindGroup*)existing);
            }

            BindGroupEntry imageEntry = new()
            {
                Binding = 0,
                Buffer = null,
                Offset = 0,
                Size = 0,
                Sampler = null,
                TextureView = view,
            };

            BindGroupDescriptor imageDesc = new()
            {
                Layout = _imageBindGroupLayout,
                EntryCount = 1,
                Entries = &imageEntry
            };

            var bindGroup = _wgpu.DeviceCreateBindGroup(_device, in imageDesc);
            _textureBindGroups[id] = (IntPtr)bindGroup;
        }

        public void UnbindImGuiTextureView(TextureView* view)
        {
            if (_textureBindGroups.Remove((IntPtr)view, out var bindGroup))
            {
                _wgpu.BindGroupRelease((BindGroup*)bindGroup);
            }
        }

        private void Init()
        {
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);

            SDL.GetWindowSize(_window, out var logicalWidth, out var localHeight);
            SDL.GetWindowSizeInPixels(_window, out var width, out var height);

            ImGui.GetIO().DisplaySize = new Vector2(logicalWidth, localHeight);
            ImGui.GetIO().DisplayFramebufferScale = new Vector2((float)width / logicalWidth, (float)height / localHeight);

            ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset | ImGuiBackendFlags.RendererHasTextures;

            InitShaders();
            InitSampler();
            InitBindGroupLayouts();
            InitPipeline();
            InitUniformBuffers();
            InitBindGroups();

            SetPerFrameImGuiData(1f / 60f);
        }

        private void InitShaders()
        {
            var src = SilkMarshal.StringToPtr(EmbeddedResources.ReadAllText("XNOEdit/Shaders/ImGui.wgsl"));
            var shaderName = SilkMarshal.StringToPtr("ImGui Shader");

            ShaderModuleWGSLDescriptor wgslDescriptor = new()
            {
                Code = (byte*)src,
                Chain = new ChainedStruct(sType: SType.ShaderModuleWgslDescriptor)
            };

            ShaderModuleDescriptor descriptor = new()
            {
                Label = (byte*)shaderName,
                NextInChain = (ChainedStruct*)(&wgslDescriptor)
            };

            _shaderModule = _wgpu.DeviceCreateShaderModule(_device, in descriptor);

            SilkMarshal.Free(src);
            SilkMarshal.Free(shaderName);
        }

        private void InitSampler()
        {
            SamplerDescriptor samplerDescriptor = new()
            {
                MinFilter = FilterMode.Linear,
                MagFilter = FilterMode.Linear,
                MipmapFilter = MipmapFilterMode.Linear,
                AddressModeU = AddressMode.Repeat,
                AddressModeV = AddressMode.Repeat,
                AddressModeW = AddressMode.Repeat,
                MaxAnisotropy = 1,
            };

            _fontSampler = _wgpu.DeviceCreateSampler(_device, in samplerDescriptor);
        }

        private void InitBindGroupLayouts()
        {
            var commonBgLayoutEntries = stackalloc BindGroupLayoutEntry[2];

            commonBgLayoutEntries[0] = new BindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
                Buffer = new BufferBindingLayout { Type = BufferBindingType.Uniform },
            };

            commonBgLayoutEntries[1] = new BindGroupLayoutEntry
            {
                Binding = 1,
                Visibility = ShaderStage.Fragment,
                Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering },
            };

            var imageBgLayoutEntries = stackalloc BindGroupLayoutEntry[1];

            imageBgLayoutEntries[0] = new BindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = ShaderStage.Fragment,
                Texture = new TextureBindingLayout
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D
                },
            };

            BindGroupLayoutDescriptor commonBgLayoutDesc = new()
            {
                EntryCount = 2,
                Entries = commonBgLayoutEntries,
            };

            BindGroupLayoutDescriptor imageBgLayoutDesc = new()
            {
                EntryCount = 1,
                Entries = imageBgLayoutEntries,
            };

            _commonBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, in commonBgLayoutDesc);
            _imageBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, in imageBgLayoutDesc);
        }

        private void InitPipeline()
        {
            if (_renderPipeline != null)
                _wgpu.RenderPipelineRelease(_renderPipeline);

            var vertexAttrib = stackalloc VertexAttribute[3];

            vertexAttrib[0] = new VertexAttribute
            {
                Format = VertexFormat.Float32x3,
                Offset = (ulong)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.Pos)),
                ShaderLocation = 0
            };

            vertexAttrib[1] = new VertexAttribute
            {
                Format = VertexFormat.Float32x3,
                Offset = (ulong)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.Uv)),
                ShaderLocation = 1
            };

            vertexAttrib[2] = new VertexAttribute
            {
                Format = VertexFormat.Unorm8x4,
                Offset = (ulong)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.Col)),
                ShaderLocation = 2
            };

            VertexBufferLayout vbLayout = new()
            {
                ArrayStride = (ulong)sizeof(ImDrawVert),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 3,
                Attributes = vertexAttrib
            };

            var pipelineBuilder = new RenderPipelineBuilder(_wgpu, _device)
                .WithBindGroupLayout(_commonBindGroupLayout)
                .WithBindGroupLayout(_imageBindGroupLayout)
                .WithCustomBlend(new BlendState
                {
                    Color = new BlendComponent
                    {
                        Operation = BlendOperation.Add, SrcFactor = BlendFactor.SrcAlpha,
                        DstFactor = BlendFactor.OneMinusSrcAlpha
                    },
                    Alpha = new BlendComponent
                    {
                        Operation = BlendOperation.Add, SrcFactor = BlendFactor.One,
                        DstFactor = BlendFactor.OneMinusSrcAlpha
                    }
                })
                .WithTopology(PrimitiveTopology.TriangleList)
                .WithCulling(CullMode.None)
                .WithVertexLayout(vbLayout)
                .WithShader(_shaderModule);

            _renderPipeline = pipelineBuilder;
        }

        private void InitUniformBuffers()
        {
            _uniformsBuffer = WgpuBuffer<Uniforms>.CreateUniform(_wgpu, _device);
        }

        private void InitBindGroups()
        {
            var bindGroupEntries = stackalloc BindGroupEntry[2];

            bindGroupEntries[0] = new BindGroupEntry
            {
                Binding = 0,
                Buffer = _uniformsBuffer.Handle,
                Offset = 0,
                Size = (ulong)Align(sizeof(Uniforms), 16),
                Sampler = null
            };

            bindGroupEntries[1] = new BindGroupEntry
            {
                Binding = 1,
                Buffer = null,
                Offset = 0,
                Size = 0,
                Sampler = _fontSampler
            };

            BindGroupDescriptor bgCommonDesc = new()
            {
                Layout = _commonBindGroupLayout,
                EntryCount = 2,
                Entries = bindGroupEntries
            };

            _commonBindGroup = _wgpu.DeviceCreateBindGroup(_device, in bgCommonDesc);
        }

        private void ProcessTextureRequest(ImTextureDataPtr texture)
        {
            switch (texture.Status)
            {
                case ImTextureStatus.WantCreate:
                    CreateTexture(texture);
                    break;
                case ImTextureStatus.WantUpdates:
                    UpdateTexture(texture);
                    break;
                case ImTextureStatus.WantDestroy:
                    DestroyTexture(texture);
                    break;
            }
        }

        private void CreateTexture(ImTextureDataPtr tex)
        {
            var width = tex.Width;
            var height = tex.Height;
            var pixels = (byte*)tex.GetPixels();

            TextureDescriptor textureDescriptor = new()
            {
                Dimension = TextureDimension.Dimension2D,
                Size = new Extent3D
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    DepthOrArrayLayers = 1,
                },
                SampleCount = 1,
                Format = TextureFormat.Rgba8Unorm,
                MipLevelCount = 1,
                Usage = TextureUsage.CopyDst | TextureUsage.TextureBinding
            };

            var texture = _wgpu.DeviceCreateTexture(_device, in textureDescriptor);

            TextureViewDescriptor textureViewDescriptor = new()
            {
                Dimension = TextureViewDimension.Dimension2D,
                Format = TextureFormat.Rgba8Unorm,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            var view = _wgpu.TextureCreateView(texture, in textureViewDescriptor);

            ImageCopyTexture imageCopyTexture = new()
            {
                Texture = texture,
                MipLevel = 0,
                Aspect = TextureAspect.All,
            };

            TextureDataLayout textureDataLayout = new()
            {
                Offset = 0,
                BytesPerRow = (uint)(width * 4),
                RowsPerImage = (uint)height,
            };

            Extent3D extent = new()
            {
                Height = (uint)height,
                Width = (uint)width,
                DepthOrArrayLayers = 1,
            };

            _wgpu.QueueWriteTexture(_queue, &imageCopyTexture, pixels, (nuint)(width * height * 4), in textureDataLayout, in extent);

            BindImGuiTextureView(view);

            _gpuTextures[(IntPtr)view] = (IntPtr)texture;

            tex.SetTexID(view);
            tex.SetStatus(ImTextureStatus.Ok);
        }

        private void UpdateTexture(ImTextureDataPtr tex)
        {
            // For partial updates - usually just recreate for simplicity
            DestroyTexture(tex);
            CreateTexture(tex);
        }

        private void DestroyTexture(ImTextureDataPtr tex)
        {
            var id = (nint)tex.TexID;

            if (id != 0)
            {
                UnbindImGuiTextureView((TextureView*)id);

                if (_gpuTextures.Remove(id, out var texture))
                {
                    _wgpu.TextureViewRelease((TextureView*)id);
                    _wgpu.TextureDestroy((Texture*)texture);
                    _wgpu.TextureRelease((Texture*)texture);
                }
            }

            tex.SetTexID(null);
            tex.SetStatus(ImTextureStatus.Destroyed);
        }

        private static bool TryMapKeys(SDL.Keycode key, out ImGuiKey imguiKey)
        {
            imguiKey = key switch
            {
                SDL.Keycode.Tab => ImGuiKey.Tab,
                SDL.Keycode.Left => ImGuiKey.LeftArrow,
                SDL.Keycode.Right => ImGuiKey.RightArrow,
                SDL.Keycode.Up => ImGuiKey.UpArrow,
                SDL.Keycode.Down => ImGuiKey.DownArrow,
                SDL.Keycode.Pageup => ImGuiKey.PageUp,
                SDL.Keycode.Pagedown => ImGuiKey.PageDown,
                SDL.Keycode.Home => ImGuiKey.Home,
                SDL.Keycode.End => ImGuiKey.End,
                SDL.Keycode.Insert => ImGuiKey.Insert,
                SDL.Keycode.Delete => ImGuiKey.Delete,
                SDL.Keycode.Backspace => ImGuiKey.Backspace,
                SDL.Keycode.Space => ImGuiKey.Space,
                SDL.Keycode.Return => ImGuiKey.Enter,
                SDL.Keycode.Escape => ImGuiKey.Escape,
                SDL.Keycode.Apostrophe => ImGuiKey.Apostrophe,
                SDL.Keycode.Comma => ImGuiKey.Comma,
                SDL.Keycode.Minus => ImGuiKey.Minus,
                SDL.Keycode.Period => ImGuiKey.Period,
                SDL.Keycode.Slash => ImGuiKey.Slash,
                SDL.Keycode.Semicolon => ImGuiKey.Semicolon,
                SDL.Keycode.Equals => ImGuiKey.Equal,
                SDL.Keycode.LeftBracket => ImGuiKey.LeftBracket,
                SDL.Keycode.Backslash => ImGuiKey.Backslash,
                SDL.Keycode.RightBracket => ImGuiKey.RightBracket,
                SDL.Keycode.Grave => ImGuiKey.GraveAccent,
                SDL.Keycode.Capslock => ImGuiKey.CapsLock,
                SDL.Keycode.ScrollLock => ImGuiKey.ScrollLock,
                SDL.Keycode.NumLockClear => ImGuiKey.NumLock,
                SDL.Keycode.PrintScreen => ImGuiKey.PrintScreen,
                SDL.Keycode.Pause => ImGuiKey.Pause,
                SDL.Keycode.Kp0 => ImGuiKey.Keypad0,
                SDL.Keycode.Kp1 => ImGuiKey.Keypad1,
                SDL.Keycode.Kp2 => ImGuiKey.Keypad2,
                SDL.Keycode.Kp3 => ImGuiKey.Keypad3,
                SDL.Keycode.Kp4 => ImGuiKey.Keypad4,
                SDL.Keycode.Kp5 => ImGuiKey.Keypad5,
                SDL.Keycode.Kp6 => ImGuiKey.Keypad6,
                SDL.Keycode.Kp7 => ImGuiKey.Keypad7,
                SDL.Keycode.Kp8 => ImGuiKey.Keypad8,
                SDL.Keycode.Kp9 => ImGuiKey.Keypad9,
                SDL.Keycode.KpDecimal => ImGuiKey.KeypadDecimal,
                SDL.Keycode.KpDivide => ImGuiKey.KeypadDivide,
                SDL.Keycode.KpMultiply => ImGuiKey.KeypadMultiply,
                SDL.Keycode.KpMinus => ImGuiKey.KeypadSubtract,
                SDL.Keycode.KpPlus => ImGuiKey.KeypadAdd,
                SDL.Keycode.KpEnter => ImGuiKey.KeypadEnter,
                SDL.Keycode.KpEquals => ImGuiKey.KeypadEqual,
                SDL.Keycode.LCtrl => ImGuiKey.LeftCtrl,
                SDL.Keycode.LShift => ImGuiKey.LeftShift,
                SDL.Keycode.LAlt => ImGuiKey.LeftAlt,
                SDL.Keycode.LHyper => ImGuiKey.LeftSuper,
                SDL.Keycode.RCtrl => ImGuiKey.RightCtrl,
                SDL.Keycode.RShift => ImGuiKey.RightShift,
                SDL.Keycode.RAlt => ImGuiKey.RightAlt,
                SDL.Keycode.RHyper => ImGuiKey.RightSuper,
                SDL.Keycode.Menu => ImGuiKey.Menu,
                SDL.Keycode.Alpha0 => ImGuiKey.Key0,
                SDL.Keycode.Alpha1 => ImGuiKey.Key1,
                SDL.Keycode.Alpha2 => ImGuiKey.Key2,
                SDL.Keycode.Alpha3 => ImGuiKey.Key3,
                SDL.Keycode.Alpha4 => ImGuiKey.Key4,
                SDL.Keycode.Alpha5 => ImGuiKey.Key5,
                SDL.Keycode.Alpha6 => ImGuiKey.Key6,
                SDL.Keycode.Alpha7 => ImGuiKey.Key7,
                SDL.Keycode.Alpha8 => ImGuiKey.Key8,
                SDL.Keycode.Alpha9 => ImGuiKey.Key9,
                SDL.Keycode.A => ImGuiKey.A,
                SDL.Keycode.B => ImGuiKey.B,
                SDL.Keycode.C => ImGuiKey.C,
                SDL.Keycode.D => ImGuiKey.D,
                SDL.Keycode.E => ImGuiKey.E,
                SDL.Keycode.F => ImGuiKey.F,
                SDL.Keycode.G => ImGuiKey.G,
                SDL.Keycode.H => ImGuiKey.H,
                SDL.Keycode.I => ImGuiKey.I,
                SDL.Keycode.J => ImGuiKey.J,
                SDL.Keycode.K => ImGuiKey.K,
                SDL.Keycode.L => ImGuiKey.L,
                SDL.Keycode.M => ImGuiKey.M,
                SDL.Keycode.N => ImGuiKey.N,
                SDL.Keycode.O => ImGuiKey.O,
                SDL.Keycode.P => ImGuiKey.P,
                SDL.Keycode.Q => ImGuiKey.Q,
                SDL.Keycode.R => ImGuiKey.R,
                SDL.Keycode.S => ImGuiKey.S,
                SDL.Keycode.T => ImGuiKey.T,
                SDL.Keycode.U => ImGuiKey.U,
                SDL.Keycode.V => ImGuiKey.V,
                SDL.Keycode.W => ImGuiKey.W,
                SDL.Keycode.X => ImGuiKey.X,
                SDL.Keycode.Y => ImGuiKey.Y,
                SDL.Keycode.Z => ImGuiKey.Z,
                SDL.Keycode.F1 => ImGuiKey.F1,
                SDL.Keycode.F2 => ImGuiKey.F2,
                SDL.Keycode.F3 => ImGuiKey.F3,
                SDL.Keycode.F4 => ImGuiKey.F4,
                SDL.Keycode.F5 => ImGuiKey.F5,
                SDL.Keycode.F6 => ImGuiKey.F6,
                SDL.Keycode.F7 => ImGuiKey.F7,
                SDL.Keycode.F8 => ImGuiKey.F8,
                SDL.Keycode.F9 => ImGuiKey.F9,
                SDL.Keycode.F10 => ImGuiKey.F10,
                SDL.Keycode.F11 => ImGuiKey.F11,
                SDL.Keycode.F12 => ImGuiKey.F12,
                _ => ImGuiKey.None,
            };

            return imguiKey != ImGuiKey.None;
        }

        public void UpdateImGuiMouse(int button, bool down)
        {
            var io = ImGui.GetIO();

            if (button == SDL.ButtonLeft)
                io.MouseDown[0] = down;

            if (button == SDL.ButtonRight)
                io.MouseDown[1] = down;

            if (button == SDL.ButtonMiddle)
                io.MouseDown[2] = down;
        }

        public void UpdateImGuiKey(SDL.Keycode keycode, bool down)
        {
            var io = ImGui.GetIO();

            if (TryMapKeys(keycode, out var imguiKey))
            {
                io.AddKeyEvent(imguiKey, down);
            }
        }

        public void UpdateImguiInput(string input)
        {
            var io = ImGui.GetIO();
            io.AddInputCharactersUTF8(input);
        }

        public void UpdateImGuiKeyModifiers(SDL.Keymod keymod)
        {
            var io = ImGui.GetIO();

            io.AddKeyEvent(ImGuiKey.ModCtrl, (keymod & SDL.Keymod.Ctrl) != 0);
            io.AddKeyEvent(ImGuiKey.ModShift, (keymod & SDL.Keymod.Shift) != 0);
            io.AddKeyEvent(ImGuiKey.ModAlt, (keymod & SDL.Keymod.Alt) != 0);
            io.AddKeyEvent(ImGuiKey.ModSuper, (keymod & SDL.Keymod.GUI) != 0);
        }

        public void UpdateImGuiMouseMove(float x, float y)
        {
            var io = ImGui.GetIO();
            io.MousePos = new Vector2(x, y);
        }

        public void UpdateImGuiMouseWheel(float x, float y)
        {
            var io = ImGui.GetIO();
            io.MouseWheel = y;
            io.MouseWheelH = x;
        }

        private void SetPerFrameImGuiData(float deltaSeconds)
        {
            SDL.GetWindowSize(_window, out var logicalWidth, out var localHeight);
            SDL.GetWindowSizeInPixels(_window, out var width, out var height);

            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(logicalWidth, localHeight);

            if (logicalWidth > 0 && localHeight > 0)
            {
                io.DisplayFramebufferScale = new Vector2((float)width / logicalWidth, (float)height / localHeight);
            }

            io.DeltaTime = deltaSeconds;
        }

        private void DrawImGui(RenderPassEncoder* encoder)
        {
            var drawData = ImGui.GetDrawData();
            drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

            var framebufferWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
            var framebufferHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
            if (framebufferWidth <= 0 || framebufferHeight <= 0)
            {
                return;
            }

            for (var i = 0; i < drawData.Textures.Size; i++)
            {
                var texture = drawData.Textures[i];

                if (texture.Status != ImTextureStatus.Ok)
                    ProcessTextureRequest(texture);
            }

            if (_windowRenderBuffers.FrameRenderBuffers == null || _windowRenderBuffers.FrameRenderBuffers.Length == 0)
            {
                _windowRenderBuffers.Index = 0;
                _windowRenderBuffers.Count = _framesInFlight;
                _windowRenderBuffers.FrameRenderBuffers = new FrameRenderBuffer[_windowRenderBuffers.Count];
            }

            _windowRenderBuffers.Index = (_windowRenderBuffers.Index + 1) % _windowRenderBuffers.Count;
            ref var frameRenderBuffer = ref _windowRenderBuffers.FrameRenderBuffers[_windowRenderBuffers.Index];

            if (drawData.TotalVtxCount > 0)
            {
                var vertSize = (ulong)Align(drawData.TotalVtxCount * sizeof(ImDrawVert), 4);
                var indexSize = (ulong)Align(drawData.TotalIdxCount * sizeof(ushort), 4);
                CreateOrUpdateBuffers(ref frameRenderBuffer, vertSize, indexSize);

                if (frameRenderBuffer is {
                        VertexBufferMemory: not null,
                        IndexBufferMemory: not null,
                        VertexBufferGpu: not null,
                        IndexBufferGpu: not null
                    })
                {
                    var vtxDst = frameRenderBuffer.VertexBufferMemory.AsPtr<ImDrawVert>();
                    var idxDst = frameRenderBuffer.IndexBufferMemory.AsPtr<ushort>();
                    for (var n = 0; n < drawData.CmdListsCount; n++)
                    {
                        var cmdList = drawData.CmdLists[n];
                        Unsafe.CopyBlock(vtxDst, cmdList.VtxBuffer.Data, (uint)cmdList.VtxBuffer.Size * (uint)sizeof(ImDrawVert));
                        Unsafe.CopyBlock(idxDst, cmdList.IdxBuffer.Data, (uint)cmdList.IdxBuffer.Size * sizeof(ushort));
                        vtxDst += cmdList.VtxBuffer.Size;
                        idxDst += cmdList.IdxBuffer.Size;
                    }

                    frameRenderBuffer.VertexBufferGpu.UpdateData(_queue, frameRenderBuffer.VertexBufferMemory);
                    frameRenderBuffer.IndexBufferGpu.UpdateData(_queue, frameRenderBuffer.IndexBufferMemory);
                }
            }

            var io = ImGui.GetIO();
            Uniforms uniforms = new()
            {
                Mvp = Matrix4x4.CreateOrthographicOffCenter(
                    0f,
                    io.DisplaySize.X,
                    io.DisplaySize.Y,
                    0.0f,
                    -1.0f,
                    1.0f
                )
            };

            _uniformsBuffer.UpdateData(_queue, in uniforms);
            _wgpu.RenderPassEncoderSetPipeline(encoder, _renderPipeline);

            if (drawData.TotalVtxCount > 0)
            {
                if (frameRenderBuffer is {
                        VertexBufferGpu: not null,
                        IndexBufferGpu: not null
                    })
                {
                    _wgpu.RenderPassEncoderSetVertexBuffer(encoder, 0, frameRenderBuffer.VertexBufferGpu.Handle, 0, frameRenderBuffer.VertexBufferGpu.Size);
                    _wgpu.RenderPassEncoderSetIndexBuffer(encoder, frameRenderBuffer.IndexBufferGpu.Handle, IndexFormat.Uint16, 0, frameRenderBuffer.IndexBufferGpu.Size);
                    uint dynamicOffsets = 0;
                    _wgpu.RenderPassEncoderSetBindGroup(encoder, 0, _commonBindGroup, 0, in dynamicOffsets);
                }
            }

            _wgpu.RenderPassEncoderSetViewport(encoder, 0, 0, drawData.FramebufferScale.X * drawData.DisplaySize.X, drawData.FramebufferScale.Y * drawData.DisplaySize.Y, 0, 1);

            if (_textureBindGroups.Count > 0)
            {
                uint defaultOffsets = 0;
                _wgpu.RenderPassEncoderSetBindGroup(encoder, 1, (BindGroup*)_textureBindGroups.Values.First(), 0, in defaultOffsets);
            }

            var vtxOffset = 0;
            var idxOffset = 0;
            for (var n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];
                for (var i = 0; i < cmdList.CmdBuffer.Size; i++)
                {
                    var cmd = cmdList.CmdBuffer[i];
                    if (cmd.UserCallback == null)
                    {
                        if (_textureBindGroups.TryGetValue(cmd.GetTexID(), out var value))
                        {
                            uint dynamicOffsets = 0;
                            _wgpu.RenderPassEncoderSetBindGroup(encoder, 1, (BindGroup*)value, 0,
                                in dynamicOffsets);
                        }
                    }

                    Vector2 clipMin = new(cmd.ClipRect.X, cmd.ClipRect.Y);
                    Vector2 clipMax = new(cmd.ClipRect.Z, cmd.ClipRect.W);

                    if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                        continue;

                    SDL.GetWindowSizeInPixels(_window, out var width, out var height);

                    _wgpu.RenderPassEncoderSetScissorRect(encoder, (uint)clipMin.X, (uint)clipMin.Y,
                        (uint)Math.Clamp(clipMax.X - clipMin.X, 0, width),
                        (uint)Math.Clamp(clipMax.Y - clipMin.Y, 0, height));
                    _wgpu.RenderPassEncoderDrawIndexed(encoder, cmd.ElemCount, 1, (uint)(idxOffset + cmd.IdxOffset), (int)(vtxOffset + cmd.VtxOffset), 0);
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }
        }

        private void CreateOrUpdateBuffers(ref FrameRenderBuffer frameRenderBuffer, ulong vertSize, ulong indexSize)
        {
            if (frameRenderBuffer.VertexBufferGpu == null || frameRenderBuffer.VertexBufferGpu.Size < vertSize)
            {
                frameRenderBuffer.VertexBufferMemory?.Dispose();
                frameRenderBuffer.VertexBufferGpu?.Dispose();

                frameRenderBuffer.VertexBufferGpu = new WgpuBuffer<byte>(_wgpu, _device, BufferUsage.Vertex | BufferUsage.CopyDst, vertSize);
                frameRenderBuffer.VertexBufferMemory = GlobalMemory.Allocate((int)vertSize);
            }

            if (frameRenderBuffer.IndexBufferGpu == null || frameRenderBuffer.IndexBufferGpu.Size < indexSize)
            {
                frameRenderBuffer.IndexBufferMemory?.Dispose();
                frameRenderBuffer.IndexBufferGpu?.Dispose();

                frameRenderBuffer.IndexBufferGpu = new WgpuBuffer<byte>(_wgpu, _device, BufferUsage.Index | BufferUsage.CopyDst, indexSize);
                frameRenderBuffer.IndexBufferMemory = GlobalMemory.Allocate((int)indexSize);
            }
        }

        public void Dispose()
        {
            if (_windowRenderBuffers.FrameRenderBuffers != null)
            {
                foreach (var renderBuffer in _windowRenderBuffers.FrameRenderBuffers)
                {
                    renderBuffer.VertexBufferGpu?.Dispose();
                    renderBuffer.IndexBufferGpu?.Dispose();
                    renderBuffer.IndexBufferMemory?.Dispose();
                    renderBuffer.VertexBufferMemory?.Dispose();
                }
            }

            foreach (var bg in _textureBindGroups.Values)
            {
                _wgpu.BindGroupRelease((BindGroup*)bg);
            }
            _textureBindGroups.Clear();

            foreach (var entry in _gpuTextures)
            {
                _wgpu.TextureViewRelease((TextureView*)entry.Key);
                _wgpu.TextureDestroy((Texture*)entry.Value);
                _wgpu.TextureRelease((Texture*)entry.Value);
            }
            _gpuTextures.Clear();

            _wgpu.BindGroupRelease(_commonBindGroup);

            _uniformsBuffer.Dispose();

            _wgpu.RenderPipelineRelease(_renderPipeline);

            _wgpu.BindGroupLayoutRelease(_commonBindGroupLayout);
            _wgpu.BindGroupLayoutRelease(_imageBindGroupLayout);

            _wgpu.SamplerRelease(_fontSampler);

            _wgpu.ShaderModuleRelease(_shaderModule);

            if (_queue != null)
                _wgpu.QueueRelease(_queue);
        }

        private static int Align(int size, int align)
        {
            return (size + (align - 1)) & ~(align - 1);
        }

        private struct Uniforms
        {
            public Matrix4x4 Mvp;
        }

        private struct FrameRenderBuffer
        {
            public WgpuBuffer<byte>? VertexBufferGpu;
            public WgpuBuffer<byte>? IndexBufferGpu;
            public GlobalMemory? VertexBufferMemory;
            public GlobalMemory? IndexBufferMemory;
        };

        private struct WindowRenderBuffers
        {
            public uint Index;
            public uint Count;
            public FrameRenderBuffer[]? FrameRenderBuffers;
        };
    }
}
