using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using Marathon.IO.Types.FileSystem;
using Plume;
using SDL3;
using Solaris;
using Solaris.Graph;
using XNOEdit.Guest;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.ModelResolver;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Renderers;
using XNOEdit.Services;
using LogLevel = XNOEdit.Logging.LogLevel;

namespace XNOEdit
{
    internal static unsafe class Program
    {
        public static event Action? GameFolderLoaded;

        private static IntPtr _window;
        private static SlDevice _device;
        private static SlFrameGraph _graph;
        private static SlUploader _uploader;

        private static Camera? _camera;
        private static ArcFile? _shaderArchive;
        private static GuestMaterialCache? _guestMaterials;
        private static GuestShaderCache? _guestCache;
        private static readonly GuestDrawContext _guestDraw = new();
        private static Scene? _scene;
        private static GridRenderer? _grid;
        private static SkyboxRenderer? _skybox;
        private static Vector3 _modelCenter = Vector3.Zero;
        private static SceneConfig? _sceneConfig;
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
                SDL.WindowFlags.HighPixelDensity | SDL.WindowFlags.Resizable | SDL.WindowFlags.Metal);

            SDL.StartTextInput(_window);

            GameFolderLoaded += LoadGameFolderResources;

            InitializeDevice();

            _camera = new Camera();
            _grid = new GridRenderer(_device);
            _skybox = new SkyboxRenderer(_device);

            var imguiController = new ImGuiController(_device, _uploader, _window);
            UIManager = new UIManager();
            UIManager.OnLoad(imguiController, _device, _window);
            UIManager.EnvironmentPanel?.InitSunAngles(_settings);
            UIManager.ResetCameraAction += ResetCamera;
            UIManager.ObjectsPanel?.LoadObject += QueueObjectLoad;
            UIManager.StagesPanel?.LoadStage += QueueStageLoad;
            UIManager.MissionsPanel?.LoadMission += QueueMissionLoad;

            _textureManager = new TextureManager(_device);
            _fileLoader = new FileLoaderService(_device, _uploader);
            _guestCache = GuestShaderCache.Load(_device, Path.Combine(AppContext.BaseDirectory, "shaders"));

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

        private static void InitializeDevice()
        {
            _device = SlDevice.Create();

            var view = SDL.MetalCreateView(_window);

            var renderWindow = new RenderWindow
            {
                Window = (void*)SDL.GetPointerProperty(
                    SDL.GetWindowProperties(_window), SDL.Props.WindowCocoaWindowPointer, IntPtr.Zero),
                View = (void*)SDL.MetalGetLayer(view),
            };

            _graph = new SlFrameGraph(_device, renderWindow, RenderFormat.B8G8R8A8Unorm, maxFrameLatency: 2);
            _graph.SetVsyncEnabled(true);

            _uploader = new SlUploader(_device);

            Logger.Info?.PrintMsg(LogClass.Application, $"Solaris Backend: {_device.Backend} ({_device.Name})");
        }

        private static void OnRenderSettingsChanged(SettingsToggle toggle)
        {
            var alert = string.Empty;

            switch (toggle)
            {
                case SettingsToggle.ShowGrid:
                    _settings.ShowGrid = !_settings.ShowGrid;
                    alert = $"Grid: {(_settings.ShowGrid ? "ON" : "OFF")}";
                    break;
                case SettingsToggle.BackfaceCulling:
                    _settings.BackfaceCulling = !_settings.BackfaceCulling;
                    alert = $"Backface Culling: {(_settings.BackfaceCulling ? "ON" : "OFF")}";
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

            var currentStage = _scene;
            var canReuseTerrain = currentStage?.TerrainName == terrainPath && terrainPath != null;

            if (canReuseTerrain)
            {
                // Same terrain, just clear objects and load new SET
                DispatchToMainThread(() =>
                {
                    currentStage!.ClearPlaced();
                });
            }
            else if (terrainPath != null)
            {
                try
                {
                    // Different terrain, load the arc
                    _loadChain?.AddArc(ArcFiles.Win32Arc(terrainPath));
                }
                catch (Exception)
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
                    _scene = new Scene(_device, []);
                });
            }

            var physicsParams = UIManager.ObjectsPanel?.PhysicsParameters.Parameters ?? [];
            var pathParams = UIManager.ObjectsPanel?.PathParameters.Parameters ?? [];
            var resolverContext = new ResolverContext(physicsParams, pathParams, _propActors, ArcFiles.ObjectArc);

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
                    _scene?.SetObjectVisible(0, objectIndex, meshIndex, visible);
                };

                SDL.SetWindowTitle(_window, $"XNOEdit - {result.Xno.Name}");

                _textureManager.Clear();
                foreach (var tex in result.Textures)
                {
                    _textureManager.Add(tex.Name, tex.Texture);
                }

                if (result.ObjectChunk.PrimitiveLists.Count == 0)
                {
                    UIManager.TriggerAlert(AlertLevel.Warning, "XNO has no geometry");
                }

                _scene?.Dispose();
                _scene = new Scene(_device, [result.Renderer]);
                _sceneConfig = null;

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
                _textureManager.Add(tex.Name, tex.Texture);
            }

            SDL.SetWindowTitle(_window, $"XNOEdit - {result.Name}.arc");

            var renderers = result.Entries.Select(e => e.Renderer).ToArray();
            var xnos = result.Entries.Select(e => e.Xno).ToList();

            var visibility = UIManager.InitStagePanel(result.Name, xnos, renderers.ToList());

            visibility.XnoVisibilityChanged += (xnoIndex, visible) =>
            {
                _scene?.SetVisible(xnoIndex, visible);
            };

            visibility.ObjectVisibilityChanged += (xnoIndex, objectIndex, meshIndex, visible) =>
            {
                _scene?.SetObjectVisible(xnoIndex, objectIndex, meshIndex, visible);
            };

            _scene?.Dispose();
            _scene = new Scene(_device, renderers, result.Name);
            _modelCenter = Vector3.Zero;
            _sceneConfig = result.SceneConfig;

            SetModelRadius(result.MaxRadius);
        }

        private static void ApplyMissionResult(MissionLoadResult result)
        {
            if (_scene is not { } scene)
                return;

            scene.ClearPlaced();

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
                var renderer = new ModelRenderer(
                    _device,
                    group.ObjectResult.ObjectChunk,
                    group.ObjectResult.Xno.GetChunk<TextureListChunk>(),
                    group.ObjectResult.Xno.GetChunk<EffectListChunk>(),
                    _guestMaterials);

                var instances = group.Instances
                    .Select(i => Matrix4x4.CreateFromQuaternion(i.Rotation) * Matrix4x4.CreateTranslation(i.Position))
                    .ToArray();

                renderer.SetInstances(instances);

                scene.AddPlaced(group.ModelPath, renderer);

                // Add textures to texture manager
                foreach (var tex in group.ObjectResult.Textures)
                {
                    _textureManager.Add(tex.Name, tex.Texture);
                }

                loadedCount++;
                totalInstances += group.Instances.Count;
            }

            SDL.SetWindowTitle(_window, $"XNOEdit - {result.Name}");
            UIManager.InitMissionPanel(result.Name);

            Logger.Info?.PrintMsg(LogClass.Application, $"Loaded {loadedCount} object types with {totalInstances} total instances");
        }

        private static void OnRender(float deltaTime)
        {
            if (_camera == null)
            {
                return;
            }

            // Resize the viewport targets from last frame's ImGui layout, before anything
            // samples or renders to them.
            UIManager.ViewportPanel.PrepareFrame();

            var view = _camera.GetViewMatrix();
            var projection = _camera.GetProjectionMatrix(UIManager.ViewportPanel.GetAspectRatio());

            // Build the UI and finalise its draw data.
            UIManager.BuildUI(view, projection, deltaTime, _settings, _textureManager);
            ImGui.Render();

            // Servicing texture requests stages uploads, so both must complete before the
            // frame opens — a staged atlas is not readable until the flush lands.
            UIManager.Controller?.PrepareFrame();
            _uploader.Flush();

            using var frame = _graph.BeginFrame();

            if (frame == null)
            {
                return;
            }

            var viewportColor = frame.ImportTexture(UIManager.ViewportPanel.ColorTarget);
            var viewportDepth = frame.ImportTexture(UIManager.ViewportPanel.DepthTarget);
            var viewportResolve = frame.ImportTexture(UIManager.ViewportPanel.ResolveTarget);

            var scene = frame.AddPass("Scene")
                .Color(viewportColor, SlLoadOp.Clear, SlClearValue.Color(0.1f, 0.1f, 0.1f))
                .Depth(viewportDepth);

            if (UIManager.ViewportPanel.IsMultisampled)
            {
                scene.ResolveTo(viewportColor, viewportResolve);
            }

            scene.Execute(ctx =>
                {
                    _skybox?.Draw(ctx, view, projection,
                        new SkyboxParameters
                        {
                            CameraPosition = _camera.Position,
                            SunDirection = _settings.SunDirection,
                            SunColor = _settings.SunColor
                        });

                    if (_settings.ShowGrid)
                    {
                        _grid?.Draw(ctx, view, projection,
                            new GridParameters
                            {
                                Model = Matrix4x4.CreateTranslation(_modelCenter),
                                Position = _camera.Position,
                                FadeDistance = _modelRadius * 5.0f
                            });
                    }

                    _scene?.Render(ctx, view, projection,
                        new ModelParameters
                        {
                            Position = _camera.Position,
                            CullBackfaces = _settings.BackfaceCulling,
                            GuestDraw = _guestDraw,
                            TextureManager = _textureManager,
                            Scene = _sceneConfig ?? new SceneConfig(),
                            EnvMap = new SlTextureIndex()
                        });
                });

            frame.AddPass("UI")
                .Reads(viewportResolve)
                .Color(frame.SwapChainTarget, SlLoadOp.Clear, SlClearValue.Color(0.15f, 0.15f, 0.15f))
                .Execute(ctx => UIManager.Controller?.Render(ctx));
        }

        private static void OnFramebufferResize(Vector2 size)
        {
            _graph.Resize();
        }

        private static void AppQuit(IntPtr appState, SDL.AppResult result)
        {
            _loadChain?.Cancel();

            _graph?.WaitForIdle();

            _uploader?.Dispose();
            _scene?.Dispose();
            _grid?.Dispose();
            _skybox?.Dispose();

            _textureManager?.Dispose();

            UIManager?.Dispose();

            _graph?.Dispose();
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
            try
            {
                foreach (var file in ArcFiles.GameArc.EnumerateFiles("*.prop", SearchOption.AllDirectories))
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
                var techniquePath = Path.Combine(AppContext.BaseDirectory, "shaders", "shader_techniques.bin");
                var techniqueTable = GuestTechniqueTable.Load(new FileStream(techniquePath, FileMode.Open));

                _guestMaterials = new GuestMaterialCache(_device, _guestCache, techniqueTable);
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
            _loadChain = new LoadChain(_fileLoader, _guestMaterials);

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
        None
    }
}
