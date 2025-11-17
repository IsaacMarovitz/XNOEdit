using System.Numerics;
using ImGuiNET;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Pfim;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Silk.NET.Windowing;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Shaders;

namespace XNOEdit
{
    internal static unsafe class Program
    {
        private static IWindow _window;
        private static WebGPU _wgpu;
        private static Instance* _instance;
        private static Adapter* _adapter;
        private static Device* _device;
        private static Queue* _queue;
        private static Surface* _surface;
        private static TextureFormat _surfaceFormat = TextureFormat.Bgra8Unorm;
        private static Texture* _depthTexture;
        private static TextureView* _depthTextureView;

        private static IKeyboard _primaryKeyboard;
        private static ImGuiController _controller;
        private static ImGuiXnoPanel _xnoPanel;
        private static ImGuiAlertPanel _alertPanel;
        private static IInputContext _input;
        private static readonly Dictionary<string, IntPtr> Textures = [];

        private static Camera _camera;
        private static ShaderArchive _shaderArchive;
        private static Model _model;
        private static ModelRenderer _modelRenderer;
        private static GridRenderer _grid;
        private static SkyboxRenderer _skybox;
        private static bool _wireframeMode;
        private static bool _showGrid = true;
        private static bool _vertexColors = true;
        private static bool _backfaceCulling;
        private static bool _mouseCaptured;
        private static bool _xnoWindow = true;
        private static bool _environmentWindow = true;
        private static Vector3 _modelCenter = Vector3.Zero;
        private static float _modelRadius = 1.0f;
        private static Vector3 _sunDirection = Vector3.Normalize(new Vector3(0.5f, 0.5f, 0.5f));
        private static Vector3 _sunColor = Vector3.Normalize(new Vector3(1.0f, 0.95f, 0.8f));
        private static Vector2 _lastMousePosition;

        private static float _sunAzimuth;
        private static float _sunAltitude;

        private static void Main()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
                Title = "XNOEdit",
                API = GraphicsAPI.None
            };

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.FramebufferResize += OnFramebufferResize;
            _window.Closing += OnClose;
            _window.FileDrop += OnFileDrop;

            _window.Run();
            _window.Dispose();
        }

        private static void OnLoad()
        {
            _input = _window.CreateInput();
            _primaryKeyboard = _input.Keyboards.FirstOrDefault();

            if (_primaryKeyboard != null)
            {
                _primaryKeyboard.KeyDown += KeyDown;
            }

            foreach (var mouse in _input.Mice)
            {
                mouse.Cursor.CursorMode = CursorMode.Normal;
                mouse.MouseMove += OnMouseMove;
                mouse.Scroll += OnMouseWheel;
                mouse.MouseDown += OnMouseDown;
                mouse.MouseUp += OnMouseUp;
            }

            InitializeWgpu();

            _controller = new ImGuiController(_wgpu, _device, _window, _input, 2, _surfaceFormat, TextureFormat.Depth24Plus);
            _alertPanel = new ImGuiAlertPanel();
            _camera = new Camera();

            CreateDepthTexture();

            _grid = new GridRenderer(_wgpu, _device, _surfaceFormat);
            _skybox = new SkyboxRenderer(_wgpu, _device, _surfaceFormat);

            _sunAltitude = MathF.Asin(_sunDirection.Y) * 180.0f / MathF.PI;
            _sunAzimuth = MathF.Atan2(_sunDirection.Z, _sunDirection.X) * 180.0f / MathF.PI;

            if (_sunAzimuth < 0)
                _sunAzimuth += 360.0f;

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        }

        private static void InitializeWgpu()
        {
            _wgpu = WebGPU.GetApi();

            var extras = new InstanceExtras
            {
                Chain = new ChainedStruct { SType = (SType)NativeSType.STypeInstanceExtras },
                Backends = InstanceBackend.Vulkan
            };

            var instanceDesc = new InstanceDescriptor
            {
                // NextInChain = (ChainedStruct*)&extras
            };

            _instance = _wgpu.CreateInstance(&instanceDesc);
            _surface = _window.CreateWebGPUSurface(_wgpu, _instance);

            var adapterOptions = new RequestAdapterOptions
            {
                PowerPreference = PowerPreference.HighPerformance,
                CompatibleSurface = _surface
            };

            Adapter* adapter = null;
            _wgpu.InstanceRequestAdapter(
                _instance,
                &adapterOptions,
                new PfnRequestAdapterCallback((status, adapterPtr, message, userdata) =>
                {
                    if (status == RequestAdapterStatus.Success)
                    {
                        adapter = adapterPtr;
                    }
                }),
                null);

            _adapter = adapter;

            AdapterProperties adapterProps = default;
            _wgpu.AdapterGetProperties(_adapter, &adapterProps);
            Console.WriteLine($"Backend type: {adapterProps.BackendType}");

            NativeFeature[] feature = [NativeFeature.BufferBindingArray];

            fixed (NativeFeature* pFeature = feature)
            {
                var deviceDesc = new DeviceDescriptor();
                // var deviceDesc = new DeviceDescriptor
                // {
                //     RequiredFeatures = (FeatureName*)pFeature,
                //     RequiredFeatureCount = (uint)feature.Length
                // };

                Device* device = null;

                _wgpu.AdapterRequestDevice(
                    _adapter,
                    &deviceDesc,
                    new PfnRequestDeviceCallback((status, devicePtr, message, userdata) =>
                    {
                        if (status == RequestDeviceStatus.Success)
                        {
                            device = devicePtr;
                        }
                    }),
                    null);

                _device = device;
            }

            _queue = _wgpu.DeviceGetQueue(_device);

            // Configure surface
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = _device,
                Format = _surfaceFormat,
                Usage = TextureUsage.RenderAttachment,
                Width = (uint)_window.FramebufferSize.X,
                Height = (uint)_window.FramebufferSize.Y,
                PresentMode = PresentMode.Fifo,
                AlphaMode = CompositeAlphaMode.Auto
            };

            _wgpu.SurfaceConfigure(_surface, &surfaceConfig);
        }

        private static void CreateDepthTexture()
        {
            var size = _window.FramebufferSize;

            var depthTextureDesc = new TextureDescriptor
            {
                Size = new Extent3D { Width = (uint)size.X, Height = (uint)size.Y, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = TextureFormat.Depth24Plus,
                Usage = TextureUsage.RenderAttachment
            };

            _depthTexture = _wgpu.DeviceCreateTexture(_device, &depthTextureDesc);

            var depthViewDesc = new TextureViewDescriptor
            {
                Format = TextureFormat.Depth24Plus,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            _depthTextureView = _wgpu.TextureCreateView(_depthTexture, &depthViewDesc);
        }

        private static void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (button != MouseButton.Left || ImGui.GetIO().WantCaptureMouse)
            {
                return;
            }

            _mouseCaptured = true;
            mouse.Cursor.CursorMode = CursorMode.Raw;
            _lastMousePosition = default;
        }

        private static void OnMouseUp(IMouse mouse, MouseButton button)
        {
            if (button != MouseButton.Left || ImGui.GetIO().WantCaptureMouse)
            {
                return;
            }

            _mouseCaptured = false;
            mouse.Cursor.CursorMode = CursorMode.Normal;
        }

        private static void OnUpdate(double deltaTime)
        {
            _camera.ProcessKeyboard(_primaryKeyboard, (float)deltaTime);
        }

        private static void OnRender(double deltaTime)
        {
            _controller.Update((float)deltaTime);

            ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode | ImGuiDockNodeFlags.NoDockingOverCentralNode);

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Render"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("Show Grid", "G", ref _showGrid);
                    ImGui.MenuItem("Vertex Colors", "V", ref _vertexColors);
                    ImGui.MenuItem("Backface Culling", "C", ref _backfaceCulling);
                    ImGui.MenuItem("Wireframe", "F", ref _wireframeMode);

                    ImGui.Separator();

                    if (ImGui.MenuItem("Reset Camera", "R"))
                    {
                        ResetCamera();
                    }

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("XNO", null, ref _xnoWindow);
                    ImGui.MenuItem("Environment", null, ref _environmentWindow);

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            if (_environmentWindow)
            {
                ImGui.Begin("Environment");
                ImGui.SeparatorText("Sun");
                ImGui.ColorEdit3("Color", ref _sunColor, ImGuiColorEditFlags.NoInputs);

                var editedAzimuth = ImGui.SliderFloat("Azimuth", ref _sunAzimuth, 0.0f, 360.0f, "%.1f°");
                var editedAltitude = ImGui.SliderFloat("Altitude", ref _sunAltitude, 0.0f, 90.0f, "%.1f°");

                if (editedAzimuth || editedAltitude)
                {
                    var azimuthRad = _sunAzimuth * MathF.PI / 180.0f;
                    var altitudeRad = _sunAltitude * MathF.PI / 180.0f;

                    _sunDirection = new Vector3(
                        (float)(Math.Cos(altitudeRad) * Math.Cos(azimuthRad)),
                        (float)Math.Sin(altitudeRad),
                        (float)(Math.Cos(altitudeRad) * Math.Sin(azimuthRad)));
                }

                ImGui.End();
            }

            if (_xnoPanel != null)
            {
                if (_xnoWindow)
                    _xnoPanel.Render(Textures);
            }
            else
            {
                ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.Text("Drag and drop a .xno file");
                ImGui.End();
            }

            _alertPanel.Render(deltaTime);

            SurfaceTexture surfaceTexture;
            _wgpu.SurfaceGetCurrentTexture(_surface, &surfaceTexture);

            if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
            {
                return;
            }

            var textureViewDesc = new TextureViewDescriptor
            {
                Format = _surfaceFormat,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            var backbuffer = _wgpu.TextureCreateView(surfaceTexture.Texture, &textureViewDesc);

            var size = _window.FramebufferSize;
            var view = _camera.GetViewMatrix();
            var projection = _camera.GetProjectionMatrix((float)size.X / size.Y);

            var commandEncoderDesc = new CommandEncoderDescriptor();
            var encoder = _wgpu.DeviceCreateCommandEncoder(_device, &commandEncoderDesc);

            var colorAttachment = new RenderPassColorAttachment
            {
                View = backbuffer,
                LoadOp = LoadOp.Clear,
                StoreOp = StoreOp.Store,
                ClearValue = new Color { R = 0.53, G = 0.81, B = 0.92, A = 1.0 }
            };

            var depthAttachment = new RenderPassDepthStencilAttachment
            {
                View = _depthTextureView,
                DepthLoadOp = LoadOp.Clear,
                DepthStoreOp = StoreOp.Store,
                DepthClearValue = 1.0f
            };

            var renderPassDesc = new RenderPassDescriptor
            {
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
                DepthStencilAttachment = &depthAttachment
            };

            var pass = _wgpu.CommandEncoderBeginRenderPass(encoder, &renderPassDesc);

            _skybox.Draw(_queue, pass, view, projection, _sunDirection, _sunColor);

            if (_showGrid)
            {
                var gridModel = Matrix4x4.CreateTranslation(_modelCenter);
                var fadeDistance = _modelRadius * 5.0f;
                _grid.Draw(_queue, pass, view, projection, gridModel, _camera.Position, fadeDistance);
            }

            if (_model != null && _modelRenderer != null)
            {
                _modelRenderer.Draw(_queue, pass, view, projection, _sunDirection, _sunColor, _camera.Position,
                    _vertexColors ? 1.0f : 0.0f, _wireframeMode, _backfaceCulling);
            }

            _controller.Render(pass);

            _wgpu.RenderPassEncoderEnd(pass);

            var commandBuffer = _wgpu.CommandEncoderFinish(encoder, null);
            _wgpu.QueueSubmit(_queue, 1, &commandBuffer);

            _wgpu.SurfacePresent(_surface);
            _wgpu.TextureViewRelease(backbuffer);
            _wgpu.CommandBufferRelease(commandBuffer);
            _wgpu.CommandEncoderRelease(encoder);
            _wgpu.RenderPassEncoderRelease(pass);
        }

        private static void OnFramebufferResize(Vector2D<int> size)
        {
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = _device,
                Format = _surfaceFormat,
                Usage = TextureUsage.RenderAttachment,
                Width = (uint)size.X,
                Height = (uint)size.Y,
                PresentMode = PresentMode.Fifo,
                AlphaMode = CompositeAlphaMode.Auto
            };

            _wgpu.SurfaceConfigure(_surface, &surfaceConfig);

            if (_depthTextureView != null)
                _wgpu.TextureViewRelease(_depthTextureView);
            if (_depthTexture != null)
                _wgpu.TextureRelease(_depthTexture);

            CreateDepthTexture();
        }

        private static void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (!_mouseCaptured) return;

            var lookSensitivity = 0.1f;
            if (_lastMousePosition == default)
            {
                _lastMousePosition = position;
            }
            else
            {
                var xOffset = (position.X - _lastMousePosition.X) * lookSensitivity;
                var yOffset = (position.Y - _lastMousePosition.Y) * lookSensitivity;
                _lastMousePosition = position;

                _camera.ProcessMouseMove(xOffset, yOffset);
            }
        }

        private static void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            if (!ImGui.GetIO().WantCaptureMouse)
                _camera.ProcessMouseScroll(scrollWheel.Y);
        }

        private static void OnClose()
        {
            _model?.Dispose();
            _modelRenderer?.Dispose();
            _grid?.Dispose();
            _skybox?.Dispose();
            _controller?.Dispose();
            _input?.Dispose();

            foreach (var texturePtr in Textures.Values)
            {
                _wgpu.TextureViewRelease((TextureView*)texturePtr);
            }

            if (_depthTextureView != null)
                _wgpu.TextureViewRelease(_depthTextureView);

            if (_depthTexture != null)
                _wgpu.TextureRelease(_depthTexture);

            if (_surface != null)
                _wgpu.SurfaceRelease(_surface);

            if (_queue != null)
                _wgpu.QueueRelease(_queue);

            if (_device != null)
                _wgpu.DeviceRelease(_device);

            if (_adapter != null)
                _wgpu.AdapterRelease(_adapter);

            if (_instance != null)
                _wgpu.InstanceRelease(_instance);
        }

        private static void OnFileDrop(string[] files)
        {
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file);

                switch (extension)
                {
                    case ".arc":
                        _shaderArchive = new ShaderArchive(file);
                        return;
                    case ".xno":
                        ReadXno(file);
                        return;
                }
            }
        }

        private static void ReadXno(string file)
        {
            try
            {
                var xno = new NinjaNext(file);
                _xnoPanel = new ImGuiXnoPanel(xno);

                _window.Title = $"XNOEdit - {xno.Name}";

                foreach (var texturePtr in Textures.Values)
                {
                    _controller.UnbindImGuiTextureView((TextureView*)texturePtr);
                    _wgpu.TextureViewRelease((TextureView*)texturePtr);
                }

                Textures.Clear();

                var textureListChunk = xno.GetChunk<TextureListChunk>();
                if (textureListChunk != null)
                {
                    Console.WriteLine($"Loading {textureListChunk.Textures.Count} textures...");
                    foreach (var texture in textureListChunk.Textures)
                    {
                        var folderPath = Path.GetDirectoryName(file);
                        var texturePath = Path.Combine(folderPath, texture.Name);

                        if (!File.Exists(texturePath))
                        {
                            Console.WriteLine($"  Warning: Texture not found: {texture.Name}");
                            continue;
                        }

                        using var image = Pfimage.FromFile(texturePath);

                        var textureDesc = new TextureDescriptor
                        {
                            Size = new Extent3D
                            {
                                Width = (uint)image.Width,
                                Height = (uint)image.Height,
                                DepthOrArrayLayers = 1
                            },
                            MipLevelCount = 1,
                            SampleCount = 1,
                            Dimension = TextureDimension.Dimension2D,
                            Format = TextureFormat.Bgra8Unorm,
                            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
                        };

                        var wgpuTexture = _wgpu.DeviceCreateTexture(_device, &textureDesc);

                        var imageData = image.Data;
                        if (image.Format == ImageFormat.Rgb24)
                        {
                            var rgbaData = new byte[image.Width * image.Height * 4];
                            for (int i = 0, j = 0; i < imageData.Length; i += 3, j += 4)
                            {
                                rgbaData[j] = imageData[i];     // R
                                rgbaData[j + 1] = imageData[i + 1]; // G
                                rgbaData[j + 2] = imageData[i + 2]; // B
                                rgbaData[j + 3] = 255;          // A
                            }
                            imageData = rgbaData;
                        }

                        fixed (byte* pData = imageData)
                        {
                            var imageCopyTexture = new ImageCopyTexture
                            {
                                Texture = wgpuTexture,
                                MipLevel = 0,
                                Origin = new Origin3D { X = 0, Y = 0, Z = 0 },
                                Aspect = TextureAspect.All
                            };

                            var textureDataLayout = new TextureDataLayout
                            {
                                Offset = 0,
                                BytesPerRow = (uint)(image.Width * 4),
                                RowsPerImage = (uint)image.Height
                            };

                            var writeSize = new Extent3D
                            {
                                Width = (uint)image.Width,
                                Height = (uint)image.Height,
                                DepthOrArrayLayers = 1
                            };

                            _wgpu.QueueWriteTexture(_queue, &imageCopyTexture, pData, (nuint)(image.Width * image.Height * 4), &textureDataLayout, &writeSize);
                        }

                        var viewDesc = new TextureViewDescriptor
                        {
                            Format = TextureFormat.Bgra8Unorm,
                            Dimension = TextureViewDimension.Dimension2D,
                            BaseMipLevel = 0,
                            MipLevelCount = 1,
                            BaseArrayLayer = 0,
                            ArrayLayerCount = 1,
                            Aspect = TextureAspect.All
                        };

                        var textureView = _wgpu.TextureCreateView(wgpuTexture, &viewDesc);
                        _controller.BindImGuiTextureView(textureView);

                        Textures.Add(texture.Name, (IntPtr)textureView);
                    }
                }

                _model?.Dispose();
                _modelRenderer?.Dispose();
                var objectChunk = xno.GetChunk<ObjectChunk>();
                var effectChunk = xno.GetChunk<EffectListChunk>();

                if (objectChunk != null && effectChunk != null)
                {
                    _model = new Model(_wgpu, _device, objectChunk, textureListChunk, effectChunk, _shaderArchive);
                    _modelRenderer = new ModelRenderer(_wgpu, _device, _surfaceFormat, _model);

                    _modelCenter = objectChunk.Centre;
                    _modelRadius = objectChunk.Radius;

                    _camera.SetModelRadius(_modelRadius);
                    _camera.NearPlane = Math.Max(_modelRadius * 0.01f, 0.1f);
                    _camera.FarPlane = Math.Max(_modelRadius * 10.0f, 1000.0f);

                    _grid?.Dispose();
                    var gridSize = _modelRadius * 4.0f;
                    _grid = new GridRenderer(_wgpu, _device, _surfaceFormat, gridSize);

                    ResetCamera();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading file: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void ResetCamera()
        {
            var distance = Math.Max(_modelRadius * 2.5f, 10.0f);
            _camera.FrameTarget(_modelCenter, distance);
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int keyCode)
        {
            if (key == Key.Escape)
                _window.Close();

            var alert = string.Empty;

            switch (key)
            {
                case Key.F:
                    _wireframeMode = !_wireframeMode;
                    alert = $"Wireframe Mode: {(_wireframeMode ? "ON" : "OFF")}";
                    break;
                case Key.G:
                    _showGrid = !_showGrid;
                    alert = $"Grid: {(_showGrid ? "ON" : "OFF")}";
                    break;
                case Key.C:
                    _backfaceCulling = !_backfaceCulling;
                    alert = $"Backface Culling: {(_backfaceCulling ? "ON" : "OFF")}";
                    break;
                case Key.V:
                    _vertexColors = !_vertexColors;
                    alert = $"Vertex Colors: {(_vertexColors ? "ON" : "OFF")}";
                    break;
                case Key.R:
                    ResetCamera();
                    alert = "Camera Reset";
                    break;
            }

            if (alert != string.Empty)
                _alertPanel.TriggerAlert(alert);
        }
    }
}
