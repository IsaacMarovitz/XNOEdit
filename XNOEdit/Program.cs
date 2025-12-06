using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using Marathon.IO.Types.FileSystem;
using SDL3;
using Silk.NET.WebGPU;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Renderers;
using XNOEdit.Renderer.Scene;
using XNOEdit.Renderer.Wgpu;
using XNOEdit.Services;

namespace XNOEdit
{
    internal static unsafe class Program
    {
        public static event Action? GameFolderLoaded;

        private static IntPtr _window;
        private static WebGPU _wgpu;
        private static WgpuDevice _device;
        private static Queue* _queue;
        private static Texture* _depthTexture;
        private static TextureView* _depthTextureView;

        private static Camera? _camera;
        private static ArcFile? _shaderArchive;
        private static IScene? _scene;
        private static GridRenderer? _grid;
        private static SkyboxRenderer? _skybox;
        private static Vector3 _modelCenter = Vector3.Zero;
        private static float _modelRadius = 1.0f;
        private static RenderSettings _settings = new();
        private static readonly UIManager UIManager = new();
        private static FileLoaderService _fileLoader;

        private static Task<XnoLoadResult?>? _pendingXnoLoad;
        private static Task<SetLoadResult?>? _pendingSetLoad;
        private static Task<ArcLoadResult?>? _pendingArcLoad;
        private static CancellationTokenSource? _loadCts;

        private static TextureManager _textureManager;

        private static ulong _previousTick;
        private static float _deltaTime;
        private static bool _mouseCaptured;
        private static Vector2 _captureStartPosition;
        private const TextureFormat DepthTextureFormat = TextureFormat.Depth32float;

        private static void Main(string[] args)
        {
            SDL.Init(SDL.InitFlags.Video);
            SDL.SetAppMetadata("XNOEdit", "1.0.0", "com.isaacmarovitz.xnoedit");

            SDL.RunApp(args.Length, args, MainFunction, IntPtr.Zero);
        }

        private static int MainFunction(int argc, string[] argv)
        {
            return SDL.EnterAppMainCallbacks(argc, argv,
                AppInit,
                AppIter,
                AppEvent,
                AppQuit);
        }

        private static SDL.AppResult AppInit(IntPtr appstate, int argc, string[] argv)
        {
            _window = SDL.CreateWindow("XNOEdit", 1280, 720,
                SDL.WindowFlags.HighPixelDensity | SDL.WindowFlags.Resizable);

            SDL.StartTextInput(_window);

            GameFolderLoaded += LoadGameFolderResources;

            InitializeWgpu();
            CreateDepthTexture();

            _camera = new Camera();
            _grid = new GridRenderer(_wgpu, _device);
            _skybox = new SkyboxRenderer(_wgpu, _device);

            var imguiController = new ImGuiController(_wgpu, _device, _window, 2);
            UIManager.OnLoad(imguiController, _wgpu, _device);
            UIManager.InitSunAngles(_settings);
            UIManager.ResetCameraAction += ResetCamera;
            UIManager.ObjectsPanel.LoadObject += QueueObjectLoad;
            UIManager.StagesPanel.LoadStage += QueueArcLoad;

            _textureManager = new TextureManager(_wgpu, _device, imguiController);

            _fileLoader = new FileLoaderService(_wgpu, _device, (IntPtr)_queue);

            Logger.SetEnable(LogLevel.Debug, Configuration.DebugLogs);

            if (Configuration.GameFolder != null)
            {
                LoadGameFolderResources();
            }

            return SDL.AppResult.Continue;
        }

        private static SDL.AppResult AppIter(IntPtr appstate)
        {
            var diff = SDL.GetTicks() - _previousTick;
            _previousTick = SDL.GetTicks();
            _deltaTime = Math.Max((float)diff / 1000, 0.000001f);

            if (UIManager.ViewportWantsInput || !ImGui.GetIO().WantCaptureKeyboard)
                _camera?.ProcessKeyboard(_deltaTime,  _settings.CameraSensitivity);

            ProcessPendingLoads();
            OnRender(_deltaTime);

            return SDL.AppResult.Continue;
        }

        private static SDL.AppResult AppEvent(IntPtr appstate, ref SDL.Event @event)
        {
            switch (@event.Type)
            {
                case (uint)SDL.EventType.WindowResized:
                    var window = SDL.GetWindowFromEvent(@event);
                    SDL.GetWindowSizeInPixels(window, out var width, out var height);

                    OnFramebufferResize(new Vector2(width, height));
                    break;
                case (uint)SDL.EventType.DropFile:
                    var span = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)@event.Drop.Data);
                    OnFileDrop(Encoding.UTF8.GetString(span));
                    break;
                case (uint)SDL.EventType.TextInput:
                    var input = Encoding.UTF8.GetString((byte*)@event.Text.Text, 32);
                    UIManager.Controller?.KeyChar(input);
                    break;
                case (uint)SDL.EventType.KeyDown:
                    if (ImGui.GetIO().WantCaptureKeyboard) {
                        UIManager.Controller?.KeyDown(@event.Key.Key);

                        if (_mouseCaptured)
                            _camera?.UpdateKeyDown(@event.Key.Key);

                        break;
                    }

                    if (UIManager.ViewportWantsInput)
                    {
                        _camera?.UpdateKeyDown(@event.Key.Key);
                    }
                    else if (_camera?.IsKeyDown(@event.Key.Key) ?? false)
                    {
                        _camera.UpdateKeyUp(@event.Key.Key);
                    }

                    var toggle = SettingsToggle.None;

                    switch (@event.Key.Key)
                    {
                        case SDL.Keycode.F:
                            toggle = SettingsToggle.WireframeMode;
                            break;
                        case SDL.Keycode.G:
                            toggle = SettingsToggle.ShowGrid;
                            break;
                        case SDL.Keycode.C:
                            toggle = SettingsToggle.BackfaceCulling;
                            break;
                        case SDL.Keycode.V:
                            toggle = SettingsToggle.VertexColors;
                            break;
                        case SDL.Keycode.R:
                            UIManager.TriggerAlert(AlertLevel.Info, "Camera Reset");
                            ResetCamera();
                            break;
                    }

                    if (toggle != SettingsToggle.None)
                        OnRenderSettingsChanged(toggle);

                    break;
                case (uint)SDL.EventType.KeyUp:
                    _camera?.UpdateKeyUp(@event.Key.Key);
                    UIManager.Controller?.KeyUp(@event.Key.Key);
                    break;
                case (uint)SDL.EventType.MouseMotion:
                    UIManager.Controller?.UpdateImGuiMouseMove(@event.Motion.X, @event.Motion.Y);

                    if (!_mouseCaptured) break;

                    var lookSensitivity = 0.1f;

                    var xOffset = (@event.Motion.X - _captureStartPosition.X) * lookSensitivity;
                    var yOffset = (@event.Motion.Y - _captureStartPosition.Y) * lookSensitivity;

                    SDL.WarpMouseInWindow(_window, _captureStartPosition.X, _captureStartPosition.Y);

                    if (xOffset != 0 || yOffset != 0)
                        _camera?.OnMouseMove(xOffset, yOffset);

                    break;
                case (uint)SDL.EventType.MouseWheel:
                    UIManager.Controller?.UpdateImGuiMouseWheel(@event.Wheel.X, @event.Wheel.Y);

                    if (UIManager.ViewportWantsInput)
                        _camera?.ProcessMouseScroll(@event.Wheel.Y, _settings.CameraSensitivity);

                    break;
                case (uint)SDL.EventType.MouseButtonDown:
                    UIManager.Controller?.UpdateImGuiMouseDown(@event.Button.Button);

                    if (@event.Button.Button != SDL.ButtonLeft)
                        break;

                    if (!UIManager.ViewportWantsInput)
                        break;

                    _mouseCaptured = true;
                    _captureStartPosition = new Vector2(@event.Button.X, @event.Button.Y);
                    SDL.SetWindowRelativeMouseMode(_window, true);

                    break;
                case (uint)SDL.EventType.MouseButtonUp:
                    UIManager.Controller?.UpdateImGuiMouseUp(@event.Button.Button);

                    if (@event.Button.Button != SDL.ButtonLeft)
                        break;

                    _mouseCaptured = false;
                    SDL.SetWindowRelativeMouseMode(_window, false);

                    break;
                case (uint)SDL.EventType.WindowCloseRequested:
                    return SDL.AppResult.Success;
            }

            return SDL.AppResult.Continue;
        }

        private static void InitializeWgpu()
        {
            _wgpu = WebGPU.GetApi();
            _device = new WgpuDevice(_wgpu, _window);
            _queue = _wgpu.DeviceGetQueue(_device);
        }

        private static void CreateDepthTexture()
        {
            SDL.GetWindowSize(_window, out var width, out var height);

            var depthTextureDesc = new TextureDescriptor
            {
                Size = new Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = DepthTextureFormat,
                Usage = TextureUsage.RenderAttachment
            };

            _depthTexture = _wgpu.DeviceCreateTexture(_device, &depthTextureDesc);

            var depthViewDesc = new TextureViewDescriptor
            {
                Format = DepthTextureFormat,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            _depthTextureView = _wgpu.TextureCreateView(_depthTexture, &depthViewDesc);
        }

        private static void OnRenderSettingsChanged(SettingsToggle toggle)
        {
            var alert = string.Empty;

            switch (toggle)
            {
                case SettingsToggle.WireframeMode:
                    _settings.WireframeMode = !_settings.WireframeMode;
                    alert = $"Wireframe Mode: {(_settings.WireframeMode ? "ON" : "OFF")}";
                    break;
                case SettingsToggle.ShowGrid:
                    _settings.ShowGrid = !_settings.ShowGrid;
                    alert = $"Grid: {(_settings.ShowGrid ? "ON" : "OFF")}";
                    break;
                case SettingsToggle.BackfaceCulling:
                    _settings.BackfaceCulling = !_settings.BackfaceCulling;
                    alert = $"Backface Culling: {(_settings.BackfaceCulling ? "ON" : "OFF")}";
                    break;
                case SettingsToggle.VertexColors:
                    _settings.VertexColors = !_settings.VertexColors;
                    alert = $"Vertex Colors: {(_settings.VertexColors ? "ON" : "OFF")}";
                    break;
                case SettingsToggle.Lightmap:
                    _settings.Lightmap = !_settings.Lightmap;
                    alert = $"Lightmap: {(_settings.Lightmap ? "ON" : "OFF")}";
                    break;
            }

            UIManager.TriggerAlert(AlertLevel.Info, alert);
        }

        private static void ProcessPendingLoads()
        {
            if (_pendingXnoLoad is { IsCompleted: true })
            {
                try
                {
                    var result = _pendingXnoLoad.Result;
                    if (result != null)
                        ApplyXnoResult(result);
                }
                catch (AggregateException ex)
                {
                    var innerEx = ex.InnerException ?? ex;
                    UIManager.TriggerAlert(AlertLevel.Error, $"Error loading XNO: \"{innerEx.Message}\"");
                    Logger.Error?.PrintStack(LogClass.Application, "Error loading XNO");
                }
                finally
                {
                    _pendingXnoLoad = null;
                    UIManager.CurrentLoadProgress = null;
                }
            }

            if (_pendingSetLoad is { IsCompleted: true })
            {
                try
                {
                    var result = _pendingSetLoad.Result;
                    if (result != null)
                        ApplySetResult(result);
                }
                catch (AggregateException ex)
                {
                    var innerEx = ex.InnerException ?? ex;
                    UIManager.TriggerAlert(AlertLevel.Error, $"Error loading SET: \"{innerEx.Message}\"");
                    Logger.Error?.PrintStack(LogClass.Application, "Error loading SET");
                }
                finally
                {
                    _pendingSetLoad = null;
                    UIManager.CurrentLoadProgress = null;
                }
            }

            if (_pendingArcLoad is { IsCompleted: true })
            {
                try
                {
                    var result = _pendingArcLoad.Result;
                    if (result != null)
                        ApplyArcResult(result);
                }
                catch (AggregateException ex)
                {
                    var innerEx = ex.InnerException ?? ex;
                    UIManager.TriggerAlert(AlertLevel.Error, $"Error loading arc: \"{innerEx.Message}\"");
                    Logger.Error?.PrintStack(LogClass.Application, "Error loading arc");
                }
                finally
                {
                    _pendingArcLoad = null;
                    UIManager.CurrentLoadProgress = null;
                }
            }
        }

        private static void ApplyXnoResult(XnoLoadResult result)
        {
            if (result.ObjectChunk != null && result.Renderer != null)
            {
                var visibility = UIManager.InitXnoPanel(result.Xno);

                visibility.VisibilityChanged += (objectIndex, meshIndex, visible) =>
                {
                    if (_scene is ObjectScene objectScene)
                    {
                        objectScene.SetVisible(objectIndex, meshIndex, visible);
                    }
                };

                SDL.SetWindowTitle(_window, $"XNOEdit - {result.Xno.Name}");

                _textureManager.Clear();
                foreach (var tex in result.Textures)
                {
                    _textureManager.Add(tex.Name, tex.Texture, tex.View);
                }

                if (result.ObjectChunk.PrimitiveLists.Count == 0)
                {
                    UIManager.TriggerAlert(AlertLevel.Warning, "XNO has no geometry");
                }

                _scene?.Dispose();
                _scene = new ObjectScene(result.Renderer);

                _modelCenter = result.ObjectChunk.Centre;
                SetModelRadius(result.ObjectChunk.Radius);
            }
            else
            {
                UIManager.TriggerAlert(AlertLevel.Error, "XNO lacks an object chunk");
            }
        }

        private static void ApplySetResult(SetLoadResult result)
        {
            var parameters = UIManager.ObjectsPanel.ObjectParameters.Parameters;

            foreach (var setObject in result.Set.Objects)
            {
                if (setObject.Type is "objectphysics_item" or "objectphysics")
                {
                    var param = parameters.FirstOrDefault(x => x.Name == (string)setObject.Parameters[0].Value);

                    if (param == null)
                        Logger.Debug?.PrintMsg(LogClass.Application, $"Missing Model {setObject.Name} of type {setObject.Type}");
                    else
                        Logger.Debug?.PrintMsg(LogClass.Application, $"Model: {param.Model}");
                }
            }
        }

        private static void ApplyArcResult(ArcLoadResult result)
        {
            _textureManager.Clear();
            foreach (var tex in result.Textures)
            {
                _textureManager.Add(tex.Name, tex.Texture, tex.View);
            }

            SDL.SetWindowTitle(_window, $"XNOEdit - {result.Name}");

            var renderers = result.Entries.Select(e => e.Renderer).ToArray();
            var xnos = result.Entries.Select(e => e.Xno).ToList();

            var visibility = UIManager.InitStagePanel(result.Name, xnos);

            visibility.XnoVisibilityChanged += (xnoIndex, visible) =>
            {
                if (_scene is StageScene stageScene)
                {
                    stageScene.SetVisible(xnoIndex, visible);
                }
            };

            visibility.ObjectVisibilityChanged += (xnoIndex, objectIndex, meshIndex, visible) =>
            {
                if (_scene is StageScene stageScene)
                {
                    stageScene.SetObjectVisible(xnoIndex, objectIndex, meshIndex, visible);
                }
            };

            _scene?.Dispose();
            _scene = new StageScene(renderers);
            _modelCenter = Vector3.Zero;

            SetModelRadius(result.MaxRadius);
        }

        private static void OnRender(double deltaTime)
        {
            SurfaceTexture surfaceTexture;
            _wgpu.SurfaceGetCurrentTexture(_device.GetSurface(), &surfaceTexture);

            if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
            {
                return;
            }

            if (_camera == null)
            {
                return;
            }

            var commandEncoderDesc = new CommandEncoderDescriptor();
            var encoder = _wgpu.DeviceCreateCommandEncoder(_device, &commandEncoderDesc);

            UIManager.ViewportPanel.PrepareFrame();

            var view = _camera.GetViewMatrix();
            var projection = _camera.GetProjectionMatrix(UIManager.ViewportPanel.GetAspectRatio());

            var scenePass = UIManager.ViewportPanel.BeginRenderPass(encoder);

            _skybox?.Draw(_queue, scenePass, view, projection,
                new SkyboxParameters
                {
                    SunDirection =  _settings.SunDirection,
                    SunColor = _settings.SunColor
                });

            if (_settings.ShowGrid)
            {
                _grid?.Draw(_queue, scenePass, view, projection,
                    new GridParameters
                    {
                        Model = Matrix4x4.CreateTranslation(_modelCenter),
                        Position = _camera.Position,
                        FadeDistance = _modelRadius * 5.0f
                    });
            }

            _scene?.Render(_queue, scenePass, view, projection,
                new ModelParameters
                {
                    SunDirection = _settings.SunDirection,
                    SunColor = _settings.SunColor,
                    Position =  _camera.Position,
                    VertColorStrength = _settings.VertexColors ? 1.0f : 0.0f,
                    Wireframe = _settings.WireframeMode,
                    CullBackfaces =  _settings.BackfaceCulling,
                    TextureManager = _textureManager,
                    Lightmap = _settings.Lightmap,
                });

            _wgpu.RenderPassEncoderEnd(scenePass);
            _wgpu.RenderPassEncoderRelease(scenePass);

            var textureViewDesc = new TextureViewDescriptor
            {
                Format = _device.GetSurfaceFormat(),
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            var backbuffer = _wgpu.TextureCreateView(surfaceTexture.Texture, &textureViewDesc);

            var uiColorAttachment = new RenderPassColorAttachment
            {
                View = backbuffer,
                LoadOp = LoadOp.Clear,
                StoreOp = StoreOp.Store,
                ClearValue = new Color { R = 0.15, G = 0.15, B = 0.15, A = 1.0 }
            };

            var uiRenderPassDesc = new RenderPassDescriptor
            {
                ColorAttachmentCount = 1,
                ColorAttachments = &uiColorAttachment,
                DepthStencilAttachment = null
            };

            var uiPass = _wgpu.CommandEncoderBeginRenderPass(encoder, &uiRenderPassDesc);

            UIManager.OnRender(
                view, projection,
                deltaTime, ref _settings, uiPass, _textureManager);

            _wgpu.RenderPassEncoderEnd(uiPass);

            var commandBuffer = _wgpu.CommandEncoderFinish(encoder, null);
            _wgpu.QueueSubmit(_queue, 1, &commandBuffer);

            _wgpu.SurfacePresent(_device.GetSurface());
            _wgpu.TextureViewRelease(backbuffer);
            _wgpu.CommandBufferRelease(commandBuffer);
            _wgpu.CommandEncoderRelease(encoder);
            _wgpu.RenderPassEncoderRelease(uiPass);
        }

        private static void OnFramebufferResize(Vector2 size)
        {
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = _device,
                Format = _device.GetSurfaceFormat(),
                Usage = TextureUsage.RenderAttachment,
                Width = (uint)size.X,
                Height = (uint)size.Y,
                PresentMode = PresentMode.Fifo,
                AlphaMode = CompositeAlphaMode.Auto
            };

            _wgpu.SurfaceConfigure(_device.GetSurface(), &surfaceConfig);

            if (_depthTextureView != null)
                _wgpu.TextureViewRelease(_depthTextureView);

            if (_depthTexture != null)
            {
                _wgpu.TextureDestroy(_depthTexture);
                _wgpu.TextureRelease(_depthTexture);
            }

            CreateDepthTexture();
        }

        private static void AppQuit(IntPtr appState, SDL.AppResult result)
        {
            SDL.DestroyWindow(_window);

            _loadCts?.Cancel();
            _loadCts?.Dispose();

            _scene?.Dispose();
            _grid?.Dispose();
            _skybox?.Dispose();

            _textureManager?.Dispose();

            UIManager?.Dispose();

            if (_depthTextureView != null)
                _wgpu.TextureViewRelease(_depthTextureView);

            if (_depthTexture != null)
            {
                _wgpu.TextureDestroy(_depthTexture);
                _wgpu.TextureRelease(_depthTexture);
            }

            if (_queue != null)
                _wgpu.QueueRelease(_queue);

            _device?.Dispose();
        }

        private static void OnFileDrop(string file)
        {
            if (!Directory.Exists(file)) return;

            var defaultXex = Path.Combine(file, "default.xex");
            if (File.Exists(defaultXex))
            {
                // We can reasonably assume this is the right folder
                Configuration.GameFolder = file;
                GameFolderLoaded?.Invoke();
            }
        }

        private static void LoadGameFolderResources()
        {
            UIManager.LoadGameFolderResources();

            var shaderArcPath = Path.Join([
                Configuration.GameFolder,
                "xenon",
                "archives",
                "shader.arc"
            ]);

            try
            {
                _shaderArchive = new ArcFile(shaderArcPath);
                UIManager.TriggerAlert(AlertLevel.Info, "Loaded shader.arc");
            }
            catch (Exception ex)
            {
                UIManager.TriggerAlert(AlertLevel.Warning, $"Unable to load shader.arc: \"{ex.Message}\"");
            }
        }

        private static void CancelPendingLoads()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            UIManager.CurrentLoadProgress = null;
        }

        private static IProgress<LoadProgress> CreateProgressReporter()
        {
            return new Progress<LoadProgress>(progress =>
            {
                UIManager.CurrentLoadProgress = progress;
            });
        }

        private static void QueueObjectLoad(IFile file)
        {
            CancelPendingLoads();
            var progress = CreateProgressReporter();

            if (file.Name.EndsWith(".xno"))
            {
                _pendingXnoLoad = _fileLoader.ReadXnoAsync(file, _shaderArchive, progress, _loadCts!.Token);
            }
            else if (file.Name.EndsWith(".set"))
            {
                _pendingSetLoad = _fileLoader.ReadSetAsync(file, progress, _loadCts!.Token);
            }
        }

        private static void QueueArcLoad(ArcFile arcFile)
        {
            CancelPendingLoads();
            var progress = CreateProgressReporter();
            _pendingArcLoad = _fileLoader.ReadArcAsync(arcFile, _shaderArchive, progress, _loadCts!.Token);
        }

        private static void SetModelRadius(float radius)
        {
            _modelRadius = radius;

            if (_camera != null)
            {
                _camera.SetModelRadius(radius);
                _camera.NearPlane = 0.01f;
                _camera.FarPlane = Math.Max(radius * 10.0f, 1000.0f);
            }

            var gridSize = radius * 4.0f;
            _grid?.Dispose();
            _grid = new GridRenderer(_wgpu, _device, gridSize);

            ResetCamera();
        }

        private static void ResetCamera()
        {
            var distance = Math.Max(_modelRadius * 2.5f, 10.0f);
            _camera?.FrameTarget(_modelCenter, distance);
        }
    }

    public enum SettingsToggle
    {
        WireframeMode,
        ShowGrid,
        BackfaceCulling,
        VertexColors,
        Lightmap,
        None
    }
}
