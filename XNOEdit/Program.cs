using System.Numerics;
using ImGuiNET;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using Marathon.IO.Types.FileSystem;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Renderers;
using XNOEdit.Renderer.Scene;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit
{
    internal static unsafe class Program
    {
        private static IWindow _window;
        private static WebGPU _wgpu;
        private static WgpuDevice _device;
        private static Queue* _queue;
        private static Texture* _depthTexture;
        private static TextureView* _depthTextureView;

        private static Camera _camera;
        private static ArcFile _shaderArchive;
        private static IScene _scene;
        private static GridRenderer _grid;
        private static SkyboxRenderer _skybox;
        private static Vector3 _modelCenter = Vector3.Zero;
        private static float _modelRadius = 1.0f;
        private static RenderSettings _settings = new();
        private static TextureManager _textureManager;
        private static readonly UIManager UIManager = new();
        private static readonly InputManager InputManager = new();

        private static IFile _pendingFileLoad;
        private static ArcFile _pendingArcFile;

        private const TextureFormat DepthTextureFormat = TextureFormat.Depth32float;

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
            InitializeWgpu();
            CreateDepthTexture();

            _camera = new Camera();
            _grid = new GridRenderer(_wgpu, _device);
            _skybox = new SkyboxRenderer(_wgpu, _device);

            InputManager.OnLoad(_window);
            InputManager.ResetCameraAction += () =>
            {
                UIManager.TriggerAlert(AlertLevel.Info, "Camera Reset");
                ResetCamera();
            };
            InputManager.SettingsChangedAction += OnRenderSettingsChanged;
            InputManager.MouseMoveAction += _camera.OnMouseMove;
            InputManager.MouseScrollAction += scrollY =>
            {
                _camera.ProcessMouseScroll(scrollY, _settings.CameraSensitivity);
            };

            UIManager.OnLoad(new ImGuiController(_wgpu, _device, _window, InputManager.Input, 2));
            UIManager.InitSunAngles(_settings);
            UIManager.ResetCameraAction += ResetCamera;
            UIManager.ObjectsPanel.LoadObject += QueueObjectLoad;
            UIManager.StagesPanel.LoadStage += QueueArcLoad;

            _textureManager = new TextureManager(_wgpu, _device, _queue, UIManager.Controller);

            Logger.SetEnable(LogLevel.Debug, Configuration.DebugLogs);

            if (Configuration.GameFolder != null)
            {
                LoadGameFolderResources();
            }
        }

        private static void ToggleSubobjectVisibility(int objectIndex, bool visibility)
        {
            if (_scene is ObjectScene objectScene)
            {
                objectScene.SetVisible(objectIndex, null, visibility);
            }
        }

        private static void ToggleMeshSetVisibility(int objectIndex, int meshIndex, bool visibility)
        {
            if (_scene is ObjectScene objectScene)
            {
                objectScene.SetVisible(objectIndex, meshIndex, visibility);
            }
        }

        private static void ToggleXnoVisibility(int xnoIndex, bool visibility)
        {
            if (_scene is StageScene stageScene)
            {
                stageScene.SetVisible(xnoIndex, visibility);
            }
        }

        private static void InitializeWgpu()
        {
            _wgpu = WebGPU.GetApi();
            _device = new WgpuDevice(_wgpu, _window);
            _queue = _wgpu.DeviceGetQueue(_device);
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

        private static void OnUpdate(double deltaTime)
        {
            if (!ImGui.GetIO().WantCaptureMouse)
                _camera.ProcessKeyboard(InputManager.PrimaryKeyboard, (float)deltaTime, _settings.CameraSensitivity);

            if (_pendingFileLoad != null)
            {
                if (_pendingFileLoad.Name.EndsWith(".xno"))
                    ReadXno(_pendingFileLoad);
                else if (_pendingFileLoad.Name.EndsWith(".set"))
                    ReadSet(_pendingFileLoad);

                _pendingFileLoad = null;
            }

            if (_pendingArcFile != null)
            {
                ReadArc(_pendingArcFile);

                _pendingArcFile = null;
            }
        }

        private static void OnRender(double deltaTime)
        {
            SurfaceTexture surfaceTexture;
            _wgpu.SurfaceGetCurrentTexture(_device.GetSurface(), &surfaceTexture);

            if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
            {
                return;
            }

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
                ClearValue = new Color { R = 1.0, G = 0.0, B = 1.0, A = 1.0 }
            };

            var depthAttachment = new RenderPassDepthStencilAttachment
            {
                View = _depthTextureView,
                DepthLoadOp = LoadOp.Clear,
                DepthStoreOp = StoreOp.Store,
                DepthClearValue = 0.0f
            };

            var renderPassDesc = new RenderPassDescriptor
            {
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
                DepthStencilAttachment = &depthAttachment
            };

            var pass = _wgpu.CommandEncoderBeginRenderPass(encoder, &renderPassDesc);

            _skybox.Draw(_queue, pass, view, projection,
                new SkyboxParameters
                {
                    SunDirection =  _settings.SunDirection,
                    SunColor = _settings.SunColor
                });

            if (_settings.ShowGrid)
            {
                _grid.Draw(_queue, pass, view, projection,
                    new GridParameters
                    {
                        Model = Matrix4x4.CreateTranslation(_modelCenter),
                        Position = _camera.Position,
                        FadeDistance = _modelRadius * 5.0f
                    });
            }

            _scene?.Render(_queue, pass, view, projection,
                new ModelParameters
                {
                    SunDirection = _settings.SunDirection,
                    SunColor = _settings.SunColor,
                    Position =  _camera.Position,
                    VertColorStrength = _settings.VertexColors ? 1.0f : 0.0f,
                    Wireframe = _settings.WireframeMode,
                    CullBackfaces =  _settings.BackfaceCulling,
                    Textures = _textureManager.Textures,
                    Lightmap = _settings.Lightmap,
                });

            UIManager.OnRender(deltaTime, ref _settings, pass, _textureManager.Textures);

            _wgpu.RenderPassEncoderEnd(pass);

            var commandBuffer = _wgpu.CommandEncoderFinish(encoder, null);
            _wgpu.QueueSubmit(_queue, 1, &commandBuffer);

            _wgpu.SurfacePresent(_device.GetSurface());
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
                _wgpu.TextureRelease(_depthTexture);

            CreateDepthTexture();
        }

        private static void OnClose()
        {
            _scene?.Dispose();
            _grid?.Dispose();
            _skybox?.Dispose();
            _textureManager?.Dispose();
            UIManager?.Dispose();
            InputManager?.Dispose();

            if (_depthTextureView != null)
                _wgpu.TextureViewRelease(_depthTextureView);

            if (_depthTexture != null)
                _wgpu.TextureRelease(_depthTexture);

            if (_queue != null)
                _wgpu.QueueRelease(_queue);

            _device?.Dispose();
        }

        private static void OnFileDrop(string[] files)
        {
            foreach (var file in files)
            {
                if (Directory.Exists(file))
                {
                    var defaultXex = Path.Combine(file, "default.xex");
                    if (File.Exists(defaultXex))
                    {
                        // We can reasonably assume this is the right folder
                        Configuration.GameFolder = file;
                        LoadGameFolderResources();
                        return;
                    }
                }
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

        private static void QueueObjectLoad(IFile file)
        {
            _pendingFileLoad = file;
        }

        private static void QueueArcLoad(ArcFile arcFile)
        {
            _pendingArcFile = arcFile;
        }

        private static void SetModelRadius(float radius)
        {
            _modelRadius = radius;

            _camera.SetModelRadius(radius);
            _camera.NearPlane = 0.01f;
            _camera.FarPlane = Math.Max(radius * 10.0f, 1000.0f);

            var gridSize = radius * 4.0f;
            _grid = new GridRenderer(_wgpu, _device, gridSize);

            ResetCamera();
        }

        private static void ResetCamera()
        {
            var distance = Math.Max(_modelRadius * 2.5f, 10.0f);
            _camera.FrameTarget(_modelCenter, distance);
        }

        private static void ReadXno(IFile file)
        {
            try
            {
                var xno = new NinjaNext(file.Decompress());

                var objectChunk = xno.GetChunk<ObjectChunk>();
                var effectChunk = xno.GetChunk<EffectListChunk>();
                var textureListChunk = xno.GetChunk<TextureListChunk>();

                if (objectChunk != null)
                {
                    UIManager.InitXnoPanel(xno, ToggleSubobjectVisibility, ToggleMeshSetVisibility);

                    _window.Title = $"XNOEdit - {xno.Name}";

                    _scene?.Dispose();
                    _grid?.Dispose();
                    _textureManager.ClearTextures();

                    _textureManager.LoadTextures(file, textureListChunk);

                    if (objectChunk.PrimitiveLists.Count == 0)
                    {
                        UIManager.TriggerAlert(AlertLevel.Warning, "XNO has no geometry");
                    }

                    var renderer  = new ModelRenderer(_wgpu, _device, objectChunk, textureListChunk, effectChunk, _shaderArchive);
                    _scene = new ObjectScene(renderer);

                    _modelCenter = objectChunk.Centre;
                    SetModelRadius(objectChunk.Radius);
                }
                else
                {
                    UIManager.TriggerAlert(AlertLevel.Error, "XNO lacks an object chunk");
                }
            }
            catch (Exception ex)
            {
                UIManager.TriggerAlert(AlertLevel.Error, $"Error loading XNO: \"{ex.Message}\"");
                Logger.Error?.PrintStack(LogClass.Application, "Error loading XNO");
            }
        }

        private static void ReadSet(IFile file)
        {
            try
            {
                var set = new StageSet(file.Decompress());
                var parameters = UIManager.ObjectsPanel.ObjectParameters.Parameters;

                foreach (var setObject in set.Objects)
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
            catch (Exception ex)
            {
                UIManager.TriggerAlert(AlertLevel.Error, $"Error loading SET: \"{ex.Message}\"");
                Logger.Error?.PrintStack(LogClass.Application, "Error loading SET");
            }
        }

        private static void ReadArc(ArcFile file)
        {
            try
            {
                List<ModelRenderer> renderers = new();
                var radius = 0f;

                _textureManager.ClearTextures();
                _grid?.Dispose();

                // TODO: Replace when ArcFile.Name is corrected
                var name = Path.GetFileName(file.Location);
                _window.Title = $"XNOEdit - {name}";
                Logger.Info?.PrintMsg(LogClass.Application, $"Loading ARC: {name}");

                var xnos = new List<NinjaNext>();

                foreach (var model in file.EnumerateFiles("*.xno", SearchOption.AllDirectories))
                {
                    var xno = new NinjaNext(model.Decompress());
                    Logger.Debug?.PrintMsg(LogClass.Application, $"Loading XNO: {xno.Name}");

                    var objectChunk = xno.GetChunk<ObjectChunk>();
                    var effectChunk = xno.GetChunk<EffectListChunk>();
                    var textureListChunk = xno.GetChunk<TextureListChunk>();

                    if (objectChunk != null)
                    {
                        _textureManager.LoadTextures(model, textureListChunk);

                        renderers.Add(new ModelRenderer(_wgpu, _device, objectChunk, textureListChunk, effectChunk, _shaderArchive));
                        radius = Math.Max(objectChunk.Radius, radius);
                    }

                    xnos.Add(xno);
                }

                UIManager.InitStagePanel(name, xnos, ToggleXnoVisibility);

                _scene = new StageScene(renderers.ToArray());
                _modelCenter = Vector3.Zero;
                SetModelRadius(radius);
            }
            catch (Exception ex)
            {
                UIManager.TriggerAlert(AlertLevel.Error, $"Error loading arc: \"{ex.Message}\"");
                Logger.Error?.PrintStack(LogClass.Application, "Error loading arc");
            }
        }
    }
}
