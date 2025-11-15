using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer
{
    public unsafe class ImGuiController : IDisposable
    {
        private readonly WebGPU _webGPU;
        private readonly Device* _device;
        private readonly Queue* _queue;
        private readonly IView _view;
        private readonly IInputContext _inputContext;
        private readonly TextureFormat _swapChainFormat;
        private readonly TextureFormat? _depthFormat;
        private readonly uint _framesInFlight;

        private ShaderModule* _shaderModule;

        private Texture* _fontTexture;
        private Sampler* _fontSampler;
        private TextureView* _fontView;

        private BindGroupLayout* _commonBindGroupLayout;
        private BindGroupLayout* _imageBindGroupLayout;
        private RenderPipeline* _renderPipeline;

        private BindGroup* _commonBindGroup;

        private Buffer* _uniformsBuffer;

        private WindowRenderBuffers _windowRenderBuffers = new();

        private readonly Dictionary<nint, nint> _viewsById = [];
        private readonly List<char> _pressedChars = [];
        private readonly Dictionary<Key, bool> _keyEvents = [];

        public ImGuiController(WebGPU webGPU, Device* device, IView view, IInputContext inputContext, uint framesInFlight, TextureFormat swapChainFormat, TextureFormat? depthFormat)
        {
            _webGPU = webGPU;
            _device = device;
            _view = view;
            _inputContext = inputContext;
            _swapChainFormat = swapChainFormat;
            _depthFormat = depthFormat;
            _framesInFlight = framesInFlight;
            _queue = _webGPU.DeviceGetQueue(_device);

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

        public BindGroup* BindImGuiTextureView(TextureView* view)
        {
            var id = (nint)view;

            if (_viewsById.TryGetValue(id, out nint ptr))
            {
                return (BindGroup*)ptr;
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

            var bindGroup = _webGPU.DeviceCreateBindGroup(_device, ref imageDesc);
            _viewsById[id] = (nint)bindGroup;

            return bindGroup;
        }

        private void Init()
        {
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);

            _inputContext.Keyboards[0].KeyUp += KeyUp;
            _inputContext.Keyboards[0].KeyDown += KeyDown;
            _inputContext.Keyboards[0].KeyChar += KeyChar;

            InitShaders();
            InitFonts();
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

            _shaderModule = _webGPU.DeviceCreateShaderModule(_device, ref descriptor);

            SilkMarshal.Free(src);
            SilkMarshal.Free(shaderName);
        }

        private void InitFonts()
        {
            ImGui.GetIO().Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int sizePerPixel);

            TextureDescriptor textureDescriptor = new()
            {
                Dimension = TextureDimension.Dimension2D,
                Size = new()
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

            _fontTexture = _webGPU.DeviceCreateTexture(_device, ref textureDescriptor);

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

            _fontView = _webGPU.TextureCreateView(_fontTexture, ref textureViewDescriptor);

            ImageCopyTexture imageCopyTexture = new()
            {
                Texture = _fontTexture,
                MipLevel = 0,
                Aspect = TextureAspect.All,
            };
            TextureDataLayout textureDataLayout = new()
            {
                Offset = 0,
                BytesPerRow = (uint)(width * sizePerPixel),
                RowsPerImage = (uint)height,
            };
            Extent3D extent = new()
            {
                Height = (uint)height,
                Width = (uint)width,
                DepthOrArrayLayers = 1,
            };

            _webGPU.QueueWriteTexture(_queue, &imageCopyTexture, pixels, (nuint)(width * height * sizePerPixel), ref textureDataLayout, ref extent);

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

            _fontSampler = _webGPU.DeviceCreateSampler(_device, ref samplerDescriptor);

            ImGui.GetIO().Fonts.SetTexID((nint)_fontView);
        }

        private void InitBindGroupLayouts()
        {
            BindGroupLayoutEntry* commonBgLayoutEntries = stackalloc BindGroupLayoutEntry[2];
            commonBgLayoutEntries[0].Binding = 0;
            commonBgLayoutEntries[0].Visibility = ShaderStage.Vertex | ShaderStage.Fragment;
            commonBgLayoutEntries[0].Buffer.Type = BufferBindingType.Uniform;
            commonBgLayoutEntries[1].Binding = 1;
            commonBgLayoutEntries[1].Visibility = ShaderStage.Fragment;
            commonBgLayoutEntries[1].Sampler.Type = SamplerBindingType.Filtering;

            BindGroupLayoutEntry* imageBgLayoutEntries = stackalloc BindGroupLayoutEntry[1];
            imageBgLayoutEntries[0].Binding = 0;
            imageBgLayoutEntries[0].Visibility = ShaderStage.Fragment;
            imageBgLayoutEntries[0].Texture.SampleType = TextureSampleType.Float;
            imageBgLayoutEntries[0].Texture.ViewDimension = TextureViewDimension.Dimension2D;

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

            _commonBindGroupLayout = _webGPU.DeviceCreateBindGroupLayout(_device, ref commonBgLayoutDesc);
            _imageBindGroupLayout = _webGPU.DeviceCreateBindGroupLayout(_device, ref imageBgLayoutDesc);
        }

        private void InitPipeline()
        {
            BindGroupLayout** bgLayouts = stackalloc BindGroupLayout*[2];
            bgLayouts[0] = _commonBindGroupLayout;
            bgLayouts[1] = _imageBindGroupLayout;
            PipelineLayoutDescriptor layoutDesc = new() { BindGroupLayoutCount = 2, BindGroupLayouts = bgLayouts };
            PipelineLayout* layout = _webGPU.DeviceCreatePipelineLayout(_device, ref layoutDesc);

            var vertexEntry = SilkMarshal.StringToPtr("vs_main");
            var fragmentEntry = SilkMarshal.StringToPtr("fs_main");

            VertexAttribute* vertexAttrib = stackalloc VertexAttribute[3];
            vertexAttrib[0].Format = VertexFormat.Float32x2;
            vertexAttrib[0].Offset = (ulong)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos));
            vertexAttrib[0].ShaderLocation = 0;
            vertexAttrib[1].Format = VertexFormat.Float32x2;
            vertexAttrib[1].Offset = (ulong)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv));
            vertexAttrib[1].ShaderLocation = 1;
            vertexAttrib[2].Format = VertexFormat.Unorm8x4;
            vertexAttrib[2].Offset = (ulong)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col));
            vertexAttrib[2].ShaderLocation = 2;

            VertexBufferLayout vbLayout = new()
            {
                ArrayStride = (ulong)sizeof(ImDrawVert),
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 3,
                Attributes = vertexAttrib
            };

            BlendState blendState = new();
            blendState.Alpha.Operation = BlendOperation.Add;
            blendState.Alpha.SrcFactor = BlendFactor.One;
            blendState.Alpha.DstFactor = BlendFactor.OneMinusSrcAlpha;
            blendState.Color.Operation = BlendOperation.Add;
            blendState.Color.SrcFactor = BlendFactor.SrcAlpha;
            blendState.Color.DstFactor = BlendFactor.OneMinusSrcAlpha;

            ColorTargetState colorTargetState = new()
            {
                Blend = &blendState,
                Format = _swapChainFormat,
                WriteMask = ColorWriteMask.All
            };

            FragmentState fragmentState = new()
            {
                Module = _shaderModule,
                EntryPoint = (byte*)fragmentEntry,
                TargetCount = 1,
                Targets = &colorTargetState
            };

            RenderPipelineDescriptor renderPipelineDescriptor = new()
            {
                Vertex = new VertexState
                {
                    Module = _shaderModule,
                    EntryPoint = (byte*)vertexEntry,
                    Buffers = &vbLayout,
                    BufferCount = 1
                },
                Primitive = new PrimitiveState
                {
                    Topology = PrimitiveTopology.TriangleList,
                    StripIndexFormat = IndexFormat.Undefined,
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.None
                },
                Multisample = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    AlphaToCoverageEnabled = false
                },
                Fragment = &fragmentState,
                Layout = layout
            };

            if (_depthFormat != null)
            {
                DepthStencilState depthStencilState = new()
                {
                    Format = (TextureFormat)_depthFormat,
                    DepthWriteEnabled = false,
                    DepthCompare = CompareFunction.Always,
                    StencilFront = new()
                    {
                        Compare = CompareFunction.Always
                    },
                    StencilBack = new()
                    {
                        Compare = CompareFunction.Always
                    }
                };

                renderPipelineDescriptor.DepthStencil = &depthStencilState;
            }

            _renderPipeline = _webGPU.DeviceCreateRenderPipeline(_device, ref renderPipelineDescriptor);

            SilkMarshal.Free(vertexEntry);
            SilkMarshal.Free(fragmentEntry);
            _webGPU.PipelineLayoutRelease(layout);
        }

        private void InitUniformBuffers()
        {
            BufferDescriptor bufferDescriptor = new()
            {
                Usage = BufferUsage.CopyDst | BufferUsage.Uniform,
                Size = (ulong)Helpers.Align(sizeof(Uniforms), 16)
            };

            _uniformsBuffer = _webGPU.DeviceCreateBuffer(_device, ref bufferDescriptor);
        }

        private void InitBindGroups()
        {
            BindGroupEntry* bindGroupEntries = stackalloc BindGroupEntry[2];
            bindGroupEntries[0].Binding = 0;
            bindGroupEntries[0].Buffer = _uniformsBuffer;
            bindGroupEntries[0].Offset = 0;
            bindGroupEntries[0].Size = (ulong)Helpers.Align(sizeof(Uniforms), 16);
            bindGroupEntries[0].Sampler = null;
            bindGroupEntries[1].Binding = 1;
            bindGroupEntries[1].Buffer = null;
            bindGroupEntries[1].Offset = 0;
            bindGroupEntries[1].Size = 0;
            bindGroupEntries[1].Sampler = _fontSampler;

            BindGroupDescriptor bgCommonDesc = new()
            {
                Layout = _commonBindGroupLayout,
                EntryCount = 2,
                Entries = bindGroupEntries
            };

            _commonBindGroup = _webGPU.DeviceCreateBindGroup(_device, ref bgCommonDesc);

            BindImGuiTextureView(_fontView);
        }

        private static bool TryMapKeys(Key key, out ImGuiKey imguikey)
        {
            imguikey = key switch
            {
                Key.Backspace => ImGuiKey.Backspace,
                Key.Tab => ImGuiKey.Tab,
                Key.Enter => ImGuiKey.Enter,
                Key.CapsLock => ImGuiKey.CapsLock,
                Key.Escape => ImGuiKey.Escape,
                Key.Space => ImGuiKey.Space,
                Key.PageUp => ImGuiKey.PageUp,
                Key.PageDown => ImGuiKey.PageDown,
                Key.End => ImGuiKey.End,
                Key.Home => ImGuiKey.Home,
                Key.Left => ImGuiKey.LeftArrow,
                Key.Right => ImGuiKey.RightArrow,
                Key.Up => ImGuiKey.UpArrow,
                Key.Down => ImGuiKey.DownArrow,
                Key.PrintScreen => ImGuiKey.PrintScreen,
                Key.Insert => ImGuiKey.Insert,
                Key.Delete => ImGuiKey.Delete,
                >= Key.Number0 and <= Key.Number9 => ImGuiKey._0 + (key - Key.Number0),
                >= Key.A and <= Key.Z => ImGuiKey.A + (key - Key.A),
                >= Key.Keypad0 and <= Key.Keypad9 => ImGuiKey.Keypad0 + (key - Key.Keypad0),
                Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
                Key.KeypadAdd => ImGuiKey.KeypadAdd,
                Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
                Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
                Key.KeypadDivide => ImGuiKey.KeypadDivide,
                Key.KeypadEqual => ImGuiKey.KeypadEqual,
                >= Key.F1 and <= Key.F1 => ImGuiKey.F1 + (key - Key.F1),
                Key.NumLock => ImGuiKey.NumLock,
                Key.ScrollLock => ImGuiKey.ScrollLock,
                Key.ShiftLeft or Key.ShiftRight => ImGuiKey.ModShift,
                Key.ControlLeft or Key.ControlRight => ImGuiKey.ModCtrl,
                Key.SuperLeft or Key.SuperRight => ImGuiKey.ModSuper,
                Key.AltLeft or Key.AltRight => ImGuiKey.ModAlt,
                Key.Semicolon => ImGuiKey.Semicolon,
                Key.Equal => ImGuiKey.Equal,
                Key.Comma => ImGuiKey.Comma,
                Key.Minus => ImGuiKey.Minus,
                Key.Period => ImGuiKey.Period,
                Key.GraveAccent => ImGuiKey.GraveAccent,
                Key.LeftBracket => ImGuiKey.LeftBracket,
                Key.RightBracket => ImGuiKey.RightBracket,
                Key.Apostrophe => ImGuiKey.Apostrophe,
                Key.Slash => ImGuiKey.Slash,
                Key.BackSlash => ImGuiKey.Backslash,
                Key.Pause => ImGuiKey.Pause,
                _ => ImGuiKey.None,
            };

            return imguikey != ImGuiKey.None;
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

            io.AddInputCharactersUTF8(CollectionsMarshal.AsSpan(_pressedChars));
            _pressedChars.Clear();

            foreach (var evt in _keyEvents)
            {
                if (TryMapKeys(evt.Key, out ImGuiKey imguikey))
                {
                    io.AddKeyEvent(imguikey, evt.Value);
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

            int framebufferWidth = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
            int framebufferHeight = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
            if (framebufferWidth <= 0 || framebufferHeight <= 0)
            {
                return;
            }

            if (_windowRenderBuffers.FrameRenderBuffers == null || _windowRenderBuffers.FrameRenderBuffers.Length == 0)
            {
                _windowRenderBuffers.Index = 0;
                _windowRenderBuffers.Count = _framesInFlight;
                _windowRenderBuffers.FrameRenderBuffers = new FrameRenderBuffer[_windowRenderBuffers.Count];
            }

            _windowRenderBuffers.Index = (_windowRenderBuffers.Index + 1) % _windowRenderBuffers.Count;
            ref FrameRenderBuffer frameRenderBuffer = ref _windowRenderBuffers.FrameRenderBuffers[_windowRenderBuffers.Index];

            if (drawData.TotalVtxCount > 0)
            {
                ulong vertSize = (ulong)Helpers.Align(drawData.TotalVtxCount * sizeof(ImDrawVert), 4);
                ulong indexSize = (ulong)Helpers.Align(drawData.TotalIdxCount * sizeof(ushort), 4);
                CreateOrUpdateBuffers(ref frameRenderBuffer, vertSize, indexSize);

                ImDrawVert* vtxDst = frameRenderBuffer.VertexBufferMemory.AsPtr<ImDrawVert>();
                ushort* idxDst = frameRenderBuffer.IndexBufferMemory.AsPtr<ushort>();
                for (int n = 0; n < drawData.CmdListsCount; n++)
                {
                    ImDrawListPtr cmd_list = drawData.CmdLists[n];
                    Unsafe.CopyBlock(vtxDst, cmd_list.VtxBuffer.Data.ToPointer(), (uint)cmd_list.VtxBuffer.Size * (uint)sizeof(ImDrawVert));
                    Unsafe.CopyBlock(idxDst, cmd_list.IdxBuffer.Data.ToPointer(), (uint)cmd_list.IdxBuffer.Size * sizeof(ushort));
                    vtxDst += cmd_list.VtxBuffer.Size;
                    idxDst += cmd_list.IdxBuffer.Size;
                }

                // Mapping might be better?
                _webGPU.QueueWriteBuffer(_queue, frameRenderBuffer.VertexBufferGpu, 0, frameRenderBuffer.VertexBufferMemory, (nuint)vertSize);
                _webGPU.QueueWriteBuffer(_queue, frameRenderBuffer.IndexBufferGpu, 0, frameRenderBuffer.IndexBufferMemory, (nuint)indexSize);
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

            _webGPU.QueueWriteBuffer(_queue, _uniformsBuffer, 0, &uniforms, (nuint)sizeof(Uniforms));

            _webGPU.RenderPassEncoderSetPipeline(encoder, _renderPipeline);

            if (drawData.TotalVtxCount > 0)
            {
                _webGPU.RenderPassEncoderSetVertexBuffer(encoder, 0, frameRenderBuffer.VertexBufferGpu, 0, frameRenderBuffer.VertexBufferSize);
                _webGPU.RenderPassEncoderSetIndexBuffer(encoder, frameRenderBuffer.IndexBufferGpu, IndexFormat.Uint16, 0, frameRenderBuffer.IndexBufferSize);
                uint dynamicOffsets = 0;
                _webGPU.RenderPassEncoderSetBindGroup(encoder, 0, _commonBindGroup, 0, ref dynamicOffsets);
            }

            _webGPU.RenderPassEncoderSetViewport(encoder, 0, 0, drawData.FramebufferScale.X * drawData.DisplaySize.X, drawData.FramebufferScale.Y * drawData.DisplaySize.Y, 0, 1);

            int vtxOffset = 0;
            int idxOffset = 0;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];
                for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
                {
                    var cmd = cmdList.CmdBuffer[i];
                    if (cmd.UserCallback != IntPtr.Zero)
                    {

                    }
                    else
                    {
                        var texId = cmd.TextureId;
                        if (texId != IntPtr.Zero)
                        {
                            if (_viewsById.TryGetValue(texId, out nint value))
                            {
                                uint dynamicOffsets = 0;
                                _webGPU.RenderPassEncoderSetBindGroup(encoder, 1, (BindGroup*)value, 0, ref dynamicOffsets);
                            }
                        }
                    }

                    Vector2 clipMin = new((cmd.ClipRect.X ), (cmd.ClipRect.Y ));
                    Vector2 clipMax = new((cmd.ClipRect.Z), (cmd.ClipRect.W ));

                    if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                        continue;

                    _webGPU.RenderPassEncoderSetScissorRect(encoder, (uint)clipMin.X, (uint)clipMin.Y, (uint)(clipMax.X - clipMin.X), (uint)(clipMax.Y - clipMin.Y));
                    _webGPU.RenderPassEncoderDrawIndexed(encoder, cmd.ElemCount, 1, (uint)(idxOffset + cmd.IdxOffset), (int)(vtxOffset + cmd.VtxOffset), 0);
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }
        }

        private void CreateOrUpdateBuffers(ref FrameRenderBuffer frameRenderBuffer, ulong vertSize, ulong indexSize)
        {
            if (frameRenderBuffer.VertexBufferGpu == null || frameRenderBuffer.VertexBufferSize < vertSize)
            {

                frameRenderBuffer.VertexBufferMemory?.Dispose();

                if (frameRenderBuffer.VertexBufferGpu != null)
                {
                    _webGPU.BufferDestroy(frameRenderBuffer.VertexBufferGpu);
                    _webGPU.BufferRelease(frameRenderBuffer.VertexBufferGpu);
                }

                BufferDescriptor desc = new()
                {
                    Size = vertSize,
                    Usage = BufferUsage.Vertex | BufferUsage.CopyDst,
                };

                frameRenderBuffer.VertexBufferGpu = _webGPU.DeviceCreateBuffer(_device, ref desc);
                frameRenderBuffer.VertexBufferSize = vertSize;
                frameRenderBuffer.VertexBufferMemory = GlobalMemory.Allocate((int)vertSize);
            }

            if (frameRenderBuffer.IndexBufferGpu == null || frameRenderBuffer.IndexBufferSize < indexSize)
            {
                frameRenderBuffer.IndexBufferMemory?.Dispose();

                if (frameRenderBuffer.IndexBufferGpu != null)
                {
                    _webGPU.BufferDestroy(frameRenderBuffer.IndexBufferGpu);
                    _webGPU.BufferRelease(frameRenderBuffer.IndexBufferGpu);
                }

                BufferDescriptor desc = new()
                {
                    Size = indexSize,
                    Usage = BufferUsage.Index | BufferUsage.CopyDst,
                };

                frameRenderBuffer.IndexBufferGpu = _webGPU.DeviceCreateBuffer(_device, ref desc);
                frameRenderBuffer.IndexBufferSize = indexSize;
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
                    _webGPU.BufferDestroy(renderBuffer.VertexBufferGpu);
                    _webGPU.BufferRelease(renderBuffer.VertexBufferGpu);
                    _webGPU.BufferDestroy(renderBuffer.IndexBufferGpu);
                    _webGPU.BufferRelease(renderBuffer.IndexBufferGpu);
                    renderBuffer.IndexBufferMemory?.Dispose();
                    renderBuffer.VertexBufferMemory?.Dispose();
                }
            }

            foreach (var bg in _viewsById)
            {
                _webGPU.BindGroupRelease((BindGroup*)bg.Value);
            }

            _webGPU.BindGroupRelease(_commonBindGroup);

            _webGPU.BufferDestroy(_uniformsBuffer);
            _webGPU.BufferRelease(_uniformsBuffer);

            _webGPU.RenderPipelineRelease(_renderPipeline);

            _webGPU.BindGroupLayoutRelease(_commonBindGroupLayout);
            _webGPU.BindGroupLayoutRelease(_imageBindGroupLayout);

            _webGPU.SamplerRelease(_fontSampler);
            _webGPU.TextureViewRelease(_fontView);
            _webGPU.TextureDestroy(_fontTexture);
            _webGPU.TextureRelease(_fontTexture);

            _webGPU.ShaderModuleRelease(_shaderModule);

            _inputContext.Keyboards[0].KeyChar -= KeyChar;
            _inputContext.Keyboards[0].KeyUp -= KeyUp;
            _inputContext.Keyboards[0].KeyDown -= KeyDown;
        }

        private class Helpers
        {
            public static int Align(int size, int align) => (((size) + ((align) - 1)) & ~((align) - 1));
        }

        private struct Uniforms
        {
            public Matrix4x4 Mvp;
        }

        internal struct FrameRenderBuffer
        {
            public ulong VertexBufferSize;
            public ulong IndexBufferSize;
            public Buffer* VertexBufferGpu;
            public Buffer* IndexBufferGpu;
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
