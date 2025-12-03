using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using XNOEdit.Renderer.Builders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    public unsafe class ImGuiController : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly WgpuDevice _device;
        private readonly Queue* _queue;
        private readonly IView _view;
        private readonly IInputContext _inputContext;
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

        private readonly List<char> _pressedChars = [];
        private readonly Dictionary<Key, bool> _keyEvents = [];

        public ImGuiController(
            WebGPU wgpu,
            WgpuDevice device,
            IView view,
            IInputContext inputContext,
            uint framesInFlight)
        {
            _wgpu = wgpu;
            _device = device;
            _view = view;
            _inputContext = inputContext;
            _framesInFlight = framesInFlight;
            _queue = _wgpu.DeviceGetQueue(_device);

            Init();
        }

        public void Update(float delta)
        {
            SetPerFrameImGuiData(delta);
            UpdateImGuiInput();
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

            ImGui.GetIO().DisplaySize = (Vector2)_view.Size;
            ImGui.GetIO().DisplayFramebufferScale = new Vector2(_view.FramebufferSize.X / _view.Size.X, _view.FramebufferSize.Y / _view.Size.Y);

            _view.Resize += (newSize) => ImGui.GetIO().DisplaySize = (Vector2)newSize;

            _inputContext.Keyboards[0].KeyUp += KeyUp;
            _inputContext.Keyboards[0].KeyDown += KeyDown;
            _inputContext.Keyboards[0].KeyChar += KeyChar;

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
                .WithShader(_shaderModule)
                .WithDepth(false, CompareFunction.Always);

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
            tex.Status = ImTextureStatus.Ok;
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
            tex.Status = ImTextureStatus.Destroyed;
        }

        private static bool TryMapKeys(Key key, out ImGuiKey imguiKey)
        {
            imguiKey = key switch
            {
                Key.Tab => ImGuiKey.Tab,
                Key.Left => ImGuiKey.LeftArrow,
                Key.Right => ImGuiKey.RightArrow,
                Key.Up => ImGuiKey.UpArrow,
                Key.Down => ImGuiKey.DownArrow,
                Key.PageUp => ImGuiKey.PageUp,
                Key.PageDown => ImGuiKey.PageDown,
                Key.Home => ImGuiKey.Home,
                Key.End => ImGuiKey.End,
                Key.Insert => ImGuiKey.Insert,
                Key.Delete => ImGuiKey.Delete,
                Key.Backspace => ImGuiKey.Backspace,
                Key.Space => ImGuiKey.Space,
                Key.Enter => ImGuiKey.Enter,
                Key.Escape => ImGuiKey.Escape,
                Key.Apostrophe => ImGuiKey.Apostrophe,
                Key.Comma => ImGuiKey.Comma,
                Key.Minus => ImGuiKey.Minus,
                Key.Period => ImGuiKey.Period,
                Key.Slash => ImGuiKey.Slash,
                Key.Semicolon => ImGuiKey.Semicolon,
                Key.Equal => ImGuiKey.Equal,
                Key.LeftBracket => ImGuiKey.LeftBracket,
                Key.BackSlash => ImGuiKey.Backslash,
                Key.RightBracket => ImGuiKey.RightBracket,
                Key.GraveAccent => ImGuiKey.GraveAccent,
                Key.CapsLock => ImGuiKey.CapsLock,
                Key.ScrollLock => ImGuiKey.ScrollLock,
                Key.NumLock => ImGuiKey.NumLock,
                Key.PrintScreen => ImGuiKey.PrintScreen,
                Key.Pause => ImGuiKey.Pause,
                Key.Keypad0 => ImGuiKey.Keypad0,
                Key.Keypad1 => ImGuiKey.Keypad1,
                Key.Keypad2 => ImGuiKey.Keypad2,
                Key.Keypad3 => ImGuiKey.Keypad3,
                Key.Keypad4 => ImGuiKey.Keypad4,
                Key.Keypad5 => ImGuiKey.Keypad5,
                Key.Keypad6 => ImGuiKey.Keypad6,
                Key.Keypad7 => ImGuiKey.Keypad7,
                Key.Keypad8 => ImGuiKey.Keypad8,
                Key.Keypad9 => ImGuiKey.Keypad9,
                Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
                Key.KeypadDivide => ImGuiKey.KeypadDivide,
                Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
                Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
                Key.KeypadAdd => ImGuiKey.KeypadAdd,
                Key.KeypadEnter => ImGuiKey.KeypadEnter,
                Key.KeypadEqual => ImGuiKey.KeypadEqual,
                Key.ControlLeft => ImGuiKey.LeftCtrl,
                Key.ShiftLeft => ImGuiKey.LeftShift,
                Key.AltLeft => ImGuiKey.LeftAlt,
                Key.SuperLeft => ImGuiKey.LeftSuper,
                Key.ControlRight => ImGuiKey.RightCtrl,
                Key.ShiftRight => ImGuiKey.RightShift,
                Key.AltRight => ImGuiKey.RightAlt,
                Key.SuperRight => ImGuiKey.RightSuper,
                Key.Menu => ImGuiKey.Menu,
                Key.Number0 => ImGuiKey.Key0,
                Key.Number1 => ImGuiKey.Key1,
                Key.Number2 => ImGuiKey.Key2,
                Key.Number3 => ImGuiKey.Key3,
                Key.Number4 => ImGuiKey.Key4,
                Key.Number5 => ImGuiKey.Key5,
                Key.Number6 => ImGuiKey.Key6,
                Key.Number7 => ImGuiKey.Key7,
                Key.Number8 => ImGuiKey.Key8,
                Key.Number9 => ImGuiKey.Key9,
                Key.A => ImGuiKey.A,
                Key.B => ImGuiKey.B,
                Key.C => ImGuiKey.C,
                Key.D => ImGuiKey.D,
                Key.E => ImGuiKey.E,
                Key.F => ImGuiKey.F,
                Key.G => ImGuiKey.G,
                Key.H => ImGuiKey.H,
                Key.I => ImGuiKey.I,
                Key.J => ImGuiKey.J,
                Key.K => ImGuiKey.K,
                Key.L => ImGuiKey.L,
                Key.M => ImGuiKey.M,
                Key.N => ImGuiKey.N,
                Key.O => ImGuiKey.O,
                Key.P => ImGuiKey.P,
                Key.Q => ImGuiKey.Q,
                Key.R => ImGuiKey.R,
                Key.S => ImGuiKey.S,
                Key.T => ImGuiKey.T,
                Key.U => ImGuiKey.U,
                Key.V => ImGuiKey.V,
                Key.W => ImGuiKey.W,
                Key.X => ImGuiKey.X,
                Key.Y => ImGuiKey.Y,
                Key.Z => ImGuiKey.Z,
                Key.F1 => ImGuiKey.F1,
                Key.F2 => ImGuiKey.F2,
                Key.F3 => ImGuiKey.F3,
                Key.F4 => ImGuiKey.F4,
                Key.F5 => ImGuiKey.F5,
                Key.F6 => ImGuiKey.F6,
                Key.F7 => ImGuiKey.F7,
                Key.F8 => ImGuiKey.F8,
                Key.F9 => ImGuiKey.F9,
                Key.F10 => ImGuiKey.F10,
                Key.F11 => ImGuiKey.F11,
                Key.F12 => ImGuiKey.F12,
                _ => ImGuiKey.None,
            };

            return imguiKey != ImGuiKey.None;
        }

        private void UpdateImGuiInput()
        {
            var io = ImGui.GetIO();

            var mouseState = _inputContext.Mice[0];
            _ = _inputContext.Keyboards[0];

            io.MouseDown[0] = mouseState.IsButtonPressed(MouseButton.Left);
            io.MouseDown[1] = mouseState.IsButtonPressed(MouseButton.Right);
            io.MouseDown[2] = mouseState.IsButtonPressed(MouseButton.Middle);

            io.MousePos = new Vector2(mouseState.Position.X, mouseState.Position.Y);

            var wheel = mouseState.ScrollWheels[0];
            io.MouseWheel = wheel.Y;
            io.MouseWheelH = wheel.X;

            io.AddInputCharactersUTF8(_pressedChars.ToString());

            _pressedChars.Clear();

            foreach (var evt in _keyEvents)
            {
                if (TryMapKeys(evt.Key, out var imguiKey))
                {
                    io.AddKeyEvent(imguiKey, evt.Value);
                }
            }
            _keyEvents.Clear();
        }

        private void SetPerFrameImGuiData(float deltaSeconds)
        {
            var io = ImGui.GetIO();
            var windowSize = _view.Size;
            io.DisplaySize = new Vector2(windowSize.X, windowSize.Y);

            if (windowSize.X > 0 && windowSize.Y > 0)
            {
                io.DisplayFramebufferScale = new Vector2(_view.FramebufferSize.X / windowSize.X, _view.FramebufferSize.Y / windowSize.Y);
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
                _wgpu.RenderPassEncoderSetVertexBuffer(encoder, 0, frameRenderBuffer.VertexBufferGpu.Handle, 0, frameRenderBuffer.VertexBufferGpu.Size);
                _wgpu.RenderPassEncoderSetIndexBuffer(encoder, frameRenderBuffer.IndexBufferGpu.Handle, IndexFormat.Uint16, 0, frameRenderBuffer.IndexBufferGpu.Size);
                uint dynamicOffsets = 0;
                _wgpu.RenderPassEncoderSetBindGroup(encoder, 0, _commonBindGroup, 0, in dynamicOffsets);
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
                    if (cmd.UserCallback != null)
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

                    _wgpu.RenderPassEncoderSetScissorRect(encoder, (uint)clipMin.X, (uint)clipMin.Y, (uint)Math.Clamp(clipMax.X - clipMin.X, 0, _view.FramebufferSize.X), (uint)Math.Clamp(clipMax.Y - clipMin.Y, 0, _view.FramebufferSize.Y));
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

        private void KeyChar(IKeyboard arg1, char arg2)
        {
            _pressedChars.Add(arg2);
        }

        private void KeyDown(IKeyboard arg1, Key arg2, int arg3)
        {
            _keyEvents[arg2] = true;
        }

        private void KeyUp(IKeyboard arg1, Key arg2, int arg3)
        {
            _keyEvents[arg2] = false;
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

            _uniformsBuffer?.Dispose();

            _wgpu.RenderPipelineRelease(_renderPipeline);

            _wgpu.BindGroupLayoutRelease(_commonBindGroupLayout);
            _wgpu.BindGroupLayoutRelease(_imageBindGroupLayout);

            _wgpu.SamplerRelease(_fontSampler);

            _wgpu.ShaderModuleRelease(_shaderModule);

            _inputContext.Keyboards[0].KeyChar -= KeyChar;
            _inputContext.Keyboards[0].KeyUp -= KeyUp;
            _inputContext.Keyboards[0].KeyDown -= KeyDown;
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
            public WgpuBuffer<byte> VertexBufferGpu;
            public WgpuBuffer<byte> IndexBufferGpu;
            public GlobalMemory VertexBufferMemory;
            public GlobalMemory IndexBufferMemory;
        };

        private struct WindowRenderBuffers
        {
            public uint Index;
            public uint Count;
            public FrameRenderBuffer[] FrameRenderBuffers;
        };
    }
}
