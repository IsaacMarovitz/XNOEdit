using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;
using Marathon.IO.Types.FileSystem;
using SDL3;
using Silk.NET.WebGPU;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.ModelResolver;
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
        private static LoadChain? _loadChain;
        private static readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private static TextureManager _textureManager;
        private static readonly List<Actor> _propActors = [];

        private static ulong _previousTick;
        private static float _deltaTime;
        private static bool _mouseCaptured;
        private static Vector2 _captureStartPosition;
        private const TextureFormat DepthTextureFormat = TextureFormat.Depth32float;

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

            InitializeWgpu();
            CreateDepthTexture();

            _camera = new Camera();
            _grid = new GridRenderer(_wgpu, _device);
            _skybox = new SkyboxRenderer(_wgpu, _device);

            var imguiController = new ImGuiController(_wgpu, _device, _window, 2);
            UIManager.OnLoad(imguiController, _wgpu, _device);
            UIManager.EnvironmentPanel?.InitSunAngles(_settings);
            UIManager.ResetCameraAction += ResetCamera;
            UIManager.ObjectsPanel?.LoadObject += QueueObjectLoad;
            UIManager.StagesPanel?.LoadStage += QueueArcLoad;
            UIManager.MissionsPanel?.LoadMission += QueueMissionLoad;

            _textureManager = new TextureManager(_wgpu, _device, imguiController);
            _fileLoader = new FileLoaderService(_wgpu, _device, (IntPtr)_queue);

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

        private static void ApplyXnoResult(XnoLoadResult result)
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

        private static void ApplyMissionResult(MissionLoadResult result)
        {
            if (_scene is not StageScene stageScene)
                return;

            stageScene.ClearInstancedRenderers();

            var objectParams = UIManager.ObjectsPanel.ObjectParameters.Parameters;

            // Group instances by model name
            var instancesByModel = new Dictionary<string, List<InstanceData>>();

            var objectArcPath = Path.Join(Configuration.GameFolder, "xenon", "archives", "object.arc");
            var objectArchive = new ArcFile(objectArcPath);

            HashSet<string> failedTypes = [];

            foreach (var setObject in result.Set.Objects)
            {
                var actor = _propActors.Find(x => x.Name == setObject.Type);

                if (actor == null)
                {
                    Logger.Warning?.PrintMsg(LogClass.Application, $"Prop actor of type {setObject.Type} not found");
                    continue;
                }

                var model = TryGetModelFromObjectName(setObject, actor, objectParams)
                            ?? TryGetModelFromPackage(setObject, objectArchive);

                if (model == null)
                {
                    failedTypes.Add(setObject.Type);
                    continue;
                }

                // TODO: Support multiple models like wvo_revolvingnet
                var first = model.FirstOrDefault();

                if (first != null)
                    AddModelInstance(first, setObject.Position, setObject.Rotation, instancesByModel);
            }

            foreach (var type in failedTypes)
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"Model for objects of type {type} not found");
            }

            if (failedTypes.Count > 0)
            {
                UIManager.TriggerAlert(AlertLevel.Warning, "Failed to find model for one of more object types");
            }

            if (instancesByModel.Count == 0 && failedTypes.Count == 0)
            {
                UIManager.TriggerAlert(AlertLevel.Warning, "SET file has no placeable objects");
                return;
            }

            // Load models and create instanced renderers
            var loadedCount = 0;
            var totalInstances = 0;

            foreach (var (modelName, instances) in instancesByModel)
            {
                var finalModelName = modelName;
                if (!Path.HasExtension(finalModelName))
                    finalModelName += ".xno";

                var modelFile = objectArchive.GetFile($"/win32/{finalModelName}");
                if (modelFile == null)
                {
                    Logger.Warning?.PrintMsg(LogClass.Application, $"Model not found: {modelName}");
                    continue;
                }

                XnoLoadResult? xnoResult = null;
                var task = _fileLoader.ReadXnoAsync(modelFile, _shaderArchive).ContinueWith(x => xnoResult =  x.Result);
                task.Wait();

                if (xnoResult?.ObjectChunk == null)
                    continue;

                var renderer = new InstancedModelRenderer(
                    _wgpu,
                    _device,
                    xnoResult.ObjectChunk,
                    xnoResult.Xno.GetChunk<TextureListChunk>(),
                    xnoResult.Xno.GetChunk<EffectListChunk>(),
                    _shaderArchive);

                renderer.SetInstances(instances.ToArray());

                stageScene.AddInstancedRenderer(modelName, renderer);

                // Add textures to texture manager
                foreach (var tex in xnoResult.Textures)
                {
                    _textureManager.Add(tex.Name, tex.Texture, tex.View);
                }

                loadedCount++;
                totalInstances += instances.Count;
            }

            var category = MissionsMap.GetMissionCategory(Path.GetFileNameWithoutExtension(result.Name));
            UIManager.SetColors(UIManager.HueForCategory(category));
            SDL.SetWindowTitle(_window, $"XNOEdit - {result.Name}");

            Logger.Info?.PrintMsg(LogClass.Application, $"Loaded {loadedCount} object types with {totalInstances} total instances");
        }

        private static string[]? TryGetModelFromObjectName(StageSetObject setObject, Actor actor, List<ObjectPhysicsParameter> objectParams)
        {
            var objectNameIndex = actor.Parameters
                .Select((param, index) => (param, index))
                .Where(pair => pair.param.Name == "objectName")
                .Select(pair => pair.index)
                .FirstOrDefault(-1);

            if (objectNameIndex == -1)
                return null;

            var modelName = (string)setObject.Parameters[objectNameIndex].Value;
            var physicsParam = objectParams.FirstOrDefault(x => x.Name == modelName);

            if (physicsParam == null)
                return null;

            return [physicsParam.Model];
        }

        private static string[]? TryGetModelFromPackage(StageSetObject setObject, ArcFile objectArchive)
        {
            if (ObjectPackagesMap.GizmoTypes.Contains(setObject.Type))
                return [];

            foreach (var group in ObjectPackagesMap.All)
            {
                foreach (var packageName in group.ObjectPackages)
                {
                    if (packageName.Key != setObject.Type)
                        continue;

                    var packageFile = objectArchive.GetFile($"/xenon/object/{group.Folder}/{packageName.Value}.pkg");
                    if (packageFile == null)
                        continue;

                    var package = new Package(packageFile.Decompress());

                    var resolver = ResolverForType(setObject.Type);
                    var matches = resolver.ResolveModel(package, setObject);

                    return matches;
                }
            }

            return null;
        }

        public static IModelResolver ResolverForType(string type)
        {
            return type switch
            {
                "common_guillotine" => new GuillotineResolver(),
                "wvo_revolvingnet" => new RevolvingNetResolver(),
                _ => new CommonResolver()
            };
        }

        private static void AddModelInstance(
            string model,
            Vector3 position,
            Quaternion rotation,
            Dictionary<string, List<InstanceData>> instancesByModel)
        {
            if (!instancesByModel.TryGetValue(model, out var instances))
            {
                instances = [];
                instancesByModel[model] = instances;
            }

            instances.Add(InstanceData.Create(position, rotation));
        }

        private static void ApplyArcResult(ArcLoadResult result)
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

            _loadChain?.Cancel();

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
                        case XnoLoadStep { Result: not null } xnoStep:
                            ApplyXnoResult(xnoStep.Result);
                            break;
                        case ArcLoadStep { Result: not null } arcStep:
                            ApplyArcResult(arcStep.Result);
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

        private static void QueueObjectLoad(IFile file)
        {
            _loadChain?.Clear();
            _loadChain?.AddXno(file);
            _loadChain?.Start();
        }

        private static void QueueArcLoad(ArcFile arcFile)
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

            _loadChain?.AddSet(setFile);
            _loadChain?.Start();
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
