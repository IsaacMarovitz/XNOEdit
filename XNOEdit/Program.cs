using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using Marathon.IO.Types.FileSystem;
using SDL3;
using Solaris;
using Solaris.Wgpu;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.ModelResolver;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Renderers;
using XNOEdit.Renderer.Scene;
using XNOEdit.Services;
using LogLevel = XNOEdit.Logging.LogLevel;

namespace XNOEdit
{
    internal static unsafe class Program
    {
        public static event Action? GameFolderLoaded;

        private static IntPtr _window;
        private static SlDevice _device;
        private static SlQueue _queue;
        private static SlTexture _depthTexture;
        private static SlTextureView _depthTextureView;

        private static Camera? _camera;
        private static ArcFile? _shaderArchive;
        private static IScene? _scene;
        private static GridRenderer? _grid;
        private static SkyboxRenderer? _skybox;
        private static Vector3 _modelCenter = Vector3.Zero;
        private static float _modelRadius = 1.0f;
        private static RenderSettings _settings = new();
        private static UIManager UIManager;
        private static FileLoaderService _fileLoader;
        private static LoadChain? _loadChain;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private static TextureManager _textureManager;
        private static readonly List<Actor> _propActors = [];

        private static ulong _previousTick;
        private static float _deltaTime;
        private static bool _mouseCaptured;
        private static Vector2 _captureStartPosition;
        private const SlTextureFormat DepthTextureFormat = SlTextureFormat.Depth32float;

        private static void Main(string[] args)
        {
            SDL.Init(SDL.InitFlags.Video);
            SDL.SetAppMetadata("XNOEdit", "1.0.0", "com.isaacmarovitz.xnoedit");

            // Don't use SDL.RunApp
            // it causes problems for the debugger
            if (AppInit(IntPtr.Zero, args.Length, args) != SDL.AppResult.Continue)
                return;

            while (true)
            {
                while (SDL.PollEvent(out var @event))
                {
                    var result = AppEvent(IntPtr.Zero, ref @event);
                    if (result != SDL.AppResult.Continue)
                    {
                        AppQuit(IntPtr.Zero, result);
                        return;
                    }
                }

                if (AppIter(IntPtr.Zero) != SDL.AppResult.Continue)
                    break;
            }

            AppQuit(IntPtr.Zero, SDL.AppResult.Success);
        }

        private static SDL.AppResult AppInit(IntPtr appState, int argc, string[] argv)
        {
            _window = SDL.CreateWindow("XNOEdit", 1280, 720,
                SDL.WindowFlags.HighPixelDensity | SDL.WindowFlags.Resizable);

            SDL.StartTextInput(_window);

            GameFolderLoaded += LoadGameFolderResources;

            WgpuBackend.Register();

            InitializeDevice(SlBackend.Wgpu);
            CreateDepthTexture();

            _camera = new Camera();
            _grid = new GridRenderer(_device);
            _skybox = new SkyboxRenderer(_device);

            var imguiController = new ImGuiController(_device, _window, 2);
            UIManager = new UIManager();
            UIManager.OnLoad(imguiController, _device);
            UIManager.EnvironmentPanel?.InitSunAngles(_settings);
            UIManager.ResetCameraAction += ResetCamera;
            UIManager.ObjectsPanel?.LoadObject += QueueObjectLoad;
            UIManager.StagesPanel?.LoadStage += QueueStageLoad;
            UIManager.MissionsPanel?.LoadMission += QueueMissionLoad;

            _textureManager = new TextureManager(imguiController);
            _fileLoader = new FileLoaderService(_device, _queue);

            Logger.SetEnable(LogLevel.Debug, Configuration.DebugLogs);

            if (Configuration.GameFolder != null)
            {
                LoadGameFolderResources();
            }

            return SDL.AppResult.Continue;
        }

        private static SDL.AppResult AppIter(IntPtr appState)
        {
            var diff = SDL.GetTicks() - _previousTick;
            _previousTick = SDL.GetTicks();
            _deltaTime = Math.Max((float)diff / 1000, 0.000001f);

            if (UIManager.ViewportWantsInput || !ImGui.GetIO().WantCaptureKeyboard)
                _camera?.ProcessKeyboard(_deltaTime, _settings.CameraSensitivity);

            ProcessMainThreadQueue();
            OnRender(_deltaTime);

            return SDL.AppResult.Continue;
        }

        private static SDL.AppResult AppEvent(IntPtr appState, ref SDL.Event @event)
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
                    UIManager.Controller?.UpdateImguiInput(input);
                    break;
                case (uint)SDL.EventType.KeyDown:
                    UIManager.Controller?.UpdateImGuiKey(@event.Key.Key, true);
                    UIManager.Controller?.UpdateImGuiKeyModifiers(@event.Key.Mod);

                    if ((UIManager.ViewportWantsInput && !ImGui.GetIO().WantCaptureKeyboard) || _mouseCaptured) {
                        _camera?.UpdateKeyDown(@event.Key.Key);
                    } else if (_camera?.IsKeyDown(@event.Key.Key) ?? false)
                    {
                        _camera.UpdateKeyUp(@event.Key.Key);
                    }

                    if (ImGui.GetIO().WantCaptureKeyboard) break;

                    var toggle = @event.Key.Key switch
                    {
                        SDL.Keycode.F => SettingsToggle.WireframeMode,
                        SDL.Keycode.G => SettingsToggle.ShowGrid,
                        SDL.Keycode.C => SettingsToggle.BackfaceCulling,
                        SDL.Keycode.V => SettingsToggle.VertexColors,
                        _ => SettingsToggle.None
                    };

                    if (toggle != SettingsToggle.None)
                        OnRenderSettingsChanged(toggle);

                    if (@event.Key.Key == SDL.Keycode.R)
                    {
                        UIManager.TriggerAlert(AlertLevel.Info, "Camera Reset");
                        ResetCamera();
                    }

                    break;
                case (uint)SDL.EventType.KeyUp:
                    _camera?.UpdateKeyUp(@event.Key.Key);
                    UIManager.Controller?.UpdateImGuiKey(@event.Key.Key, false);
                    UIManager.Controller?.UpdateImGuiKeyModifiers(@event.Key.Mod);
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
                    UIManager.Controller?.UpdateImGuiMouse(@event.Button.Button, true);

                    if (@event.Button.Button != SDL.ButtonLeft)
                        break;

                    if (!UIManager.ViewportWantsInput)
                        break;

                    _mouseCaptured = true;
                    _captureStartPosition = new Vector2(@event.Button.X, @event.Button.Y);
                    SDL.SetWindowRelativeMouseMode(_window, true);

                    break;
                case (uint)SDL.EventType.MouseButtonUp:
                    UIManager.Controller?.UpdateImGuiMouse(@event.Button.Button, false);

                    if (@event.Button.Button != SDL.ButtonLeft)
                        break;

                    _mouseCaptured = false;
                    SDL.SetWindowRelativeMouseMode(_window, false);

                    break;
                case (uint)SDL.EventType.Quit:
                case (uint)SDL.EventType.WindowCloseRequested:
                    return SDL.AppResult.Success;
            }

            return SDL.AppResult.Continue;
        }

        private static void InitializeDevice(SlBackend backend)
        {
            _device = SlDeviceFactory.Create(backend, _window);
            _queue = _device.GetQueue();

            Logger.Info?.PrintMsg(LogClass.Application, $"Solaris Backend: {backend}");
        }

        private static void CreateDepthTexture()
        {
            SDL.GetWindowSize(_window, out var width, out var height);

            var depthTextureDesc = new SlTextureDescriptor
            {
                Size = new SlExtent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = SlTextureDimension.Dimension2D,
                Format = DepthTextureFormat,
                Usage = SlTextureUsage.RenderAttachment
            };

            _depthTexture = _device.CreateTexture(depthTextureDesc);

            var depthViewDesc = new SlTextureViewDescriptor
            {
                Format = DepthTextureFormat,
                Dimension = SlTextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1
            };

            _depthTextureView = _depthTexture.CreateTextureView(depthViewDesc);
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

        private static void DispatchToMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private static void ProcessMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                action();
            }
        }

        private static void QueueObjectLoad(IFile file)
        {
            _loadChain?.Clear();
            _loadChain?.AddXno(file);
            _loadChain?.Start();
        }

        private static void QueueStageLoad(ArcFile arcFile)
        {
            _loadChain?.Clear();
            _loadChain?.AddArc(arcFile);
            _loadChain?.Start();
        }

        private static void QueueMissionLoad(IFile setFile)
        {
            _loadChain?.Clear();

            var setName = Path.GetFileNameWithoutExtension(setFile.Name);
            var terrainPath = MissionsMap.GetTerrainPath(setName);

            var currentStage = _scene as StageScene;
            var canReuseTerrain = currentStage?.TerrainName == terrainPath && terrainPath != null;

            if (canReuseTerrain)
            {
                // Same terrain, just clear objects and load new SET
                DispatchToMainThread(() =>
                {
                    currentStage!.ClearInstancedRenderers();
                });
            }
            else if (terrainPath != null)
            {
                // Different terrain, load the arc
                var fullPath = Path.Join(
                    Configuration.GameFolder,
                    "win32",
                    "archives",
                    $"{terrainPath}.arc"
                );

                if (File.Exists(fullPath))
                {
                    _loadChain?.AddArc(new ArcFile(fullPath));
                }
                else
                {
                    UIManager.TriggerAlert(AlertLevel.Warning, $"Failed to find terrain at {terrainPath}.arc");
                    return;
                }
            }
            else
            {
                // No terrain for this mission
                DispatchToMainThread(() =>
                {
                    _scene?.Dispose();
                    _scene = new StageScene([]);
                });
            }

            var objectArcPath = Path.Join(Configuration.GameFolder, "xenon", "archives", "object.arc");
            var objectArchive = new ArcFile(objectArcPath);
            var physicsParams = UIManager.ObjectsPanel?.PhysicsParameters.Parameters ?? [];
            var pathParams = UIManager.ObjectsPanel?.PathParameters.Parameters ?? [];
            var resolverContext = new ResolverContext(physicsParams, pathParams, _propActors, objectArchive);

            _loadChain?.AddSet(setFile, resolverContext);
            _loadChain?.Start();
        }

        private static void ApplyObjectResult(ObjectLoadResult result)
        {
            if (result.ObjectChunk != null && result.Renderer != null)
            {
                var visibility = UIManager.InitXnoPanel(result.Xno, result.Renderer);

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

        private static void ApplyStageResult(StageLoadResult result)
        {
            _textureManager.Clear();
            foreach (var tex in result.Textures)
            {
                _textureManager.Add(tex.Name, tex.Texture, tex.View);
            }

            SDL.SetWindowTitle(_window, $"XNOEdit - {result.Name}.arc");

            var renderers = result.Entries.Select(e => e.Renderer).ToArray();
            var xnos = result.Entries.Select(e => e.Xno).ToList();

            var visibility = UIManager.InitStagePanel(result.Name, xnos, renderers.ToList());

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
            _scene = new StageScene(renderers, result.Name);
            _modelCenter = Vector3.Zero;

            SetModelRadius(result.MaxRadius);
        }

        private static void ApplyMissionResult(MissionLoadResult result)
        {
            if (_scene is not StageScene stageScene)
                return;

            stageScene.ClearInstancedRenderers();

            foreach (var type in result.FailedTypes)
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"Model for objects of type {type} not found");
            }

            if (result.FailedTypes.Count > 0)
            {
                UIManager.TriggerAlert(AlertLevel.Warning, "Failed to find model for one or more object types");
            }

            if (result.LoadedGroups.Count == 0 && result.FailedTypes.Count == 0)
            {
                UIManager.TriggerAlert(AlertLevel.Warning, "SET file has no placeable objects");
                return;
            }

            var loadedCount = 0;
            var totalInstances = 0;

            foreach (var group in result.LoadedGroups)
            {
                var renderer = new InstancedModelRenderer(
                    _device,
                    group.ObjectResult.ObjectChunk,
                    group.ObjectResult.Xno.GetChunk<TextureListChunk>(),
                    group.ObjectResult.Xno.GetChunk<EffectListChunk>(),
                    _shaderArchive);

                var instanceData = group.Instances
                    .Select(i => InstanceData.Create(i.Position, i.Rotation))
                    .ToArray();

                renderer.SetInstances(instanceData);

                stageScene.AddInstancedRenderer(group.ModelPath, renderer);

                // Add textures to texture manager
                foreach (var tex in group.ObjectResult.Textures)
                {
                    _textureManager.Add(tex.Name, tex.Texture, tex.View);
                }

                loadedCount++;
                totalInstances += group.Instances.Count;
            }

            SDL.SetWindowTitle(_window, $"XNOEdit - {result.Name}");
            UIManager.InitMissionPanel(result.Name);

            Logger.Info?.PrintMsg(LogClass.Application, $"Loaded {loadedCount} object types with {totalInstances} total instances");
        }

        private static void OnRender(double deltaTime)
        {
            var surface = _device.GetSurface();
            var surfaceTexture = surface.GetCurrentTexture();

            if (_camera == null)
            {
                return;
            }

            var encoder = _device.CreateCommandEncoder();

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

            scenePass.End();
            scenePass.Dispose();

            var textureViewDesc = new SlTextureViewDescriptor
            {
                Format = SlDevice.SurfaceFormat,
                Dimension = SlTextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
            };

            var backbuffer = surfaceTexture.CreateTextureView(textureViewDesc);

            var uiColorAttachment = new SlColorAttachment
            {
                View = backbuffer,
                LoadOp = SlLoadOp.Clear,
                StoreOp = SlStoreOp.Store,
                ClearValue = new SlColor { R = 0.15, G = 0.15, B = 0.15, A = 1.0 }
            };

            var uiRenderPassDesc = new SlRenderPassDescriptor
            {
                ColorAttachments = [uiColorAttachment],
                DepthStencilAttachment = null
            };

            var uiPass = encoder.BeginRenderPass(uiRenderPassDesc);

            UIManager.OnRender(
                view, projection,
                deltaTime, ref _settings, uiPass, _textureManager);

            uiPass.End();

            var commandBuffer = encoder.Finish();
            _queue.Submit(commandBuffer);

            surface.Present();
            backbuffer.Dispose();
            commandBuffer.Dispose();
            encoder.Dispose();
            uiPass.Dispose();
        }

        private static void OnFramebufferResize(Vector2 size)
        {
            var surface = _device.GetSurface();
            var surfaceDescriptor = new SlSurfaceDescriptor
            {
                Format = SlDevice.SurfaceFormat,
                Usage = SlTextureUsage.RenderAttachment,
                Width = (uint)size.X,
                Height = (uint)size.Y,
                PresentMode = SlPresentMode.Fifo
            };

            surface.Configure(surfaceDescriptor);

            _depthTextureView?.Dispose();
            _depthTexture?.Dispose();

            CreateDepthTexture();
        }

        private static void AppQuit(IntPtr appState, SDL.AppResult result)
        {
            SDL.DestroyWindow(_window);

            _loadChain?.Cancel();

            _scene?.Dispose();
            _grid?.Dispose();
            _skybox?.Dispose();

            _textureManager?.Dispose();

            UIManager?.Dispose();

            _depthTextureView?.Dispose();
            _depthTexture?.Dispose();
            _queue?.Dispose();
            _device?.Dispose();

            SDL.DestroyWindow(_window);
            SDL.Quit();
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

            var shaderArcPath = Path.Join(
                Configuration.GameFolder,
                "xenon",
                "archives",
                "shader.arc"
            );

            var gameArcPath = Path.Join(
                Configuration.GameFolder,
                "xenon",
                "archives",
                "game.arc"
            );

            try
            {
                var gameArchive = new ArcFile(gameArcPath);
                foreach (var file in gameArchive.EnumerateFiles("*.prop", SearchOption.AllDirectories))
                {
                    var propLibrary = new PropLibrary(file.Decompress());
                    _propActors.AddRange(propLibrary.Actors);
                }
            }
            catch (Exception ex)
            {
                UIManager.TriggerAlert(AlertLevel.Warning, $"Unable to load prop actors: \"{ex.Message}\"");
            }

            try
            {
                _shaderArchive = new ArcFile(shaderArcPath);
                InitializeLoadChain();
                UIManager.TriggerAlert(AlertLevel.Info, "Loaded shader.arc");
            }
            catch (Exception ex)
            {
                UIManager.TriggerAlert(AlertLevel.Warning, $"Unable to load shader.arc: \"{ex.Message}\"");
            }
        }

        private static void InitializeLoadChain()
        {
            _loadChain = new LoadChain(_fileLoader, _shaderArchive);

            _loadChain.ProgressChanged += progress =>
            {
                DispatchToMainThread(() => UIManager.CurrentLoadProgress = progress);
            };

            _loadChain.StepCompleted += step =>
            {
                DispatchToMainThread(() =>
                {
                    switch (step)
                    {
                        case ObjectLoadStep { Result: not null } xnoStep:
                            ApplyObjectResult(xnoStep.Result);
                            break;
                        case StageLoadStep { Result: not null } arcStep:
                            ApplyStageResult(arcStep.Result);
                            break;
                        case MissionLoadStep { Result: not null } missionStep:
                            ApplyMissionResult(missionStep.Result);
                            break;
                    }
                });
            };

            _loadChain.ChainCompleted += () =>
            {
                DispatchToMainThread(() => UIManager.CurrentLoadProgress = null);
            };

            _loadChain.ChainFailed += ex =>
            {
                DispatchToMainThread(() =>
                {
                    UIManager.CurrentLoadProgress = null;
                    UIManager.TriggerAlert(AlertLevel.Error, $"Load failed: \"{ex.Message}\"");
                    Logger.Error?.PrintStack(LogClass.Application, "Load chain failed");
                });
            };
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
            _grid = new GridRenderer(_device, gridSize);

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
