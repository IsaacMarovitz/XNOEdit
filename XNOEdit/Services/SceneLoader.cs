using System.Collections.Concurrent;
using System.Numerics;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using Marathon.IO.Types.FileSystem;
using SDL3;
using Solaris;
using XNOEdit.Guest;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.ModelResolver;
using XNOEdit.Panels;
using XNOEdit.Render;
using XNOEdit.Render.Renderers;

namespace XNOEdit.Services
{
    public class SceneLoader
    {
        private const string DefaultEnvironmentStage = "stage_wvo_a";

        private readonly SlDevice _device;
        private readonly nint _window;
        private readonly UIManager _ui;
        private readonly SceneView _view;
        private readonly TextureManager _textureManager;
        private readonly FileLoaderService _fileLoader;
        private readonly GuestShaderCache _guestCache;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private readonly List<Actor> _propActors = [];

        private GuestMaterialCache? _guestMaterials;
        private LoadChain? _loadChain;

        public SceneLoader(
            SlDevice device,
            SlUploader uploader,
            nint window,
            UIManager ui,
            SceneView view,
            TextureManager textureManager)
        {
            _device = device;
            _window = window;
            _ui = ui;
            _view = view;
            _textureManager = textureManager;

            _fileLoader = new FileLoaderService(device, uploader);
            _guestCache = GuestShaderCache.Load(device, Path.Combine(AppContext.BaseDirectory, "shaders"));
        }

        /// <summary>Runs work that background loads handed back. Call once per frame.</summary>
        public void ProcessMainThreadQueue()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                action();
            }
        }

        public void Cancel()
        {
            _loadChain?.Cancel();
        }

        public void QueueObjectLoad(IFile file)
        {
            _loadChain?.Clear();
            _loadChain?.AddXno(file);
            _loadChain?.Start();
        }

        public void QueueStageLoad(ArcFile arcFile)
        {
            _loadChain?.Clear();
            _loadChain?.AddArc(arcFile);
            _loadChain?.Start();
        }

        public void QueueMissionLoad(IFile setFile)
        {
            _loadChain?.Clear();

            var setName = Path.GetFileNameWithoutExtension(setFile.Name);
            var terrainPath = MissionsMap.GetTerrainPath(setName);

            var currentStage = _view.Scene;
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
                    _ui.TriggerAlert(AlertLevel.Warning, $"Failed to find terrain at {terrainPath}.arc");
                    return;
                }
            }
            else
            {
                // No terrain for this mission
                DispatchToMainThread(() =>
                {
                    _view.Environment.ClearOverride();
                    _view.SetScene(new Scene(_device, []));
                });
            }

            var physicsParams = _ui.ObjectsPanel?.PhysicsParameters.Parameters ?? [];
            var pathParams = _ui.ObjectsPanel?.PathParameters.Parameters ?? [];
            var resolverContext = new ResolverContext(physicsParams, pathParams, _propActors, ArcFiles.ObjectArc);

            _loadChain?.AddSet(setFile, resolverContext);
            _loadChain?.Start();
        }

        public void LoadGameFolderResources()
        {
            _ui.LoadGameFolderResources();

            try
            {
                _ui.SetGameFonts(_fileLoader.ReadGameFonts());
            }
            catch (Exception ex)
            {
                _ui.TriggerAlert(AlertLevel.Warning, $"Unable to load text.arc: \"{ex.Message}\"");
            }

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
                _ui.TriggerAlert(AlertLevel.Warning, $"Unable to load prop actors: \"{ex.Message}\"");
            }

            try
            {
                _guestMaterials = new GuestMaterialCache(_device, _guestCache);
                InitializeLoadChain();
                _ui.TriggerAlert(AlertLevel.Info, "Loaded shader.arc");
            }
            catch (Exception ex)
            {
                _ui.TriggerAlert(AlertLevel.Warning, $"Unable to load shader.arc: \"{ex.Message}\"");
            }

            _ = LoadDefaultEnvironmentAsync();
        }

        private async Task LoadDefaultEnvironmentAsync()
        {
            try
            {
                if (await _fileLoader.ReadEnvironmentAsync(DefaultEnvironmentStage) is { } environment)
                    DispatchToMainThread(() => _view.Environment.SetDefault(environment.Config, environment.EnvMap));
            }
            catch (Exception ex)
            {
                DispatchToMainThread(() =>
                    _ui.TriggerAlert(AlertLevel.Warning, $"Unable to load default environment: \"{ex.Message}\""));
            }
        }

        private void DispatchToMainThread(Action action)
        {
            _mainThreadQueue.Enqueue(action);
        }

        private void InitializeLoadChain()
        {
            _loadChain = new LoadChain(_fileLoader, _guestMaterials);

            _loadChain.ProgressChanged += progress =>
            {
                DispatchToMainThread(() => _ui.CurrentLoadProgress = progress);
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
                DispatchToMainThread(() => _ui.CurrentLoadProgress = null);
            };

            _loadChain.ChainFailed += ex =>
            {
                DispatchToMainThread(() =>
                {
                    _ui.CurrentLoadProgress = null;
                    _ui.TriggerAlert(AlertLevel.Error, $"Load failed: \"{ex.Message}\"");
                    Logger.Error?.PrintStack(LogClass.Application, "Load chain failed");
                });
            };
        }

        private void ApplyObjectResult(ObjectLoadResult result)
        {
            if (result.ObjectChunk != null && result.Renderer != null)
            {
                var visibility = _ui.InitXnoPanel(result.Xno, result.Renderer);

                visibility.VisibilityChanged += (objectIndex, meshIndex, visible) =>
                {
                    _view.Scene?.SetObjectVisible(0, objectIndex, meshIndex, visible);
                };

                SDL.SetWindowTitle(_window, $"XNOEdit - {result.Xno.Name}");

                _textureManager.Clear();
                foreach (var tex in result.Textures)
                {
                    _textureManager.Add(tex.Name, tex.Texture);
                }

                if (result.ObjectChunk.PrimitiveLists.Count == 0)
                {
                    _ui.TriggerAlert(AlertLevel.Warning, "XNO has no geometry");
                }

                _view.Environment.ClearOverride();
                _view.SetScene(new Scene(_device, [result.Renderer]));
                _view.Frame(result.ObjectChunk.Centre, result.ObjectChunk.Radius);
            }
            else
            {
                _ui.TriggerAlert(AlertLevel.Error, "XNO lacks an object chunk");
            }
        }

        private void ApplyStageResult(StageLoadResult result)
        {
            _textureManager.Clear();
            foreach (var tex in result.Textures)
            {
                _textureManager.Add(tex.Name, tex.Texture);
            }

            SDL.SetWindowTitle(_window, $"XNOEdit - {result.Name}.arc");

            var renderers = result.Entries.Select(e => e.Renderer).ToArray();
            var xnos = result.Entries.Select(e => e.Xno).ToList();

            var visibility = _ui.InitStagePanel(result.Name, xnos, renderers.ToList());

            visibility.XnoVisibilityChanged += (xnoIndex, visible) =>
            {
                _view.Scene?.SetVisible(xnoIndex, visible);
            };

            visibility.ObjectVisibilityChanged += (xnoIndex, objectIndex, meshIndex, visible) =>
            {
                _view.Scene?.SetObjectVisible(xnoIndex, objectIndex, meshIndex, visible);
            };

            _view.Environment.Override(result.SceneConfig, result.EnvMap);
            _view.SetScene(new Scene(_device, renderers, result.Name));
            _view.Frame(Vector3.Zero, result.MaxRadius);
        }

        private void ApplyMissionResult(MissionLoadResult result)
        {
            if (_view.Scene is not { } scene)
                return;

            scene.ClearPlaced();

            foreach (var type in result.FailedTypes)
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"Model for objects of type {type} not found");
            }

            if (result.FailedTypes.Count > 0)
            {
                _ui.TriggerAlert(AlertLevel.Warning, "Failed to find model for one or more object types");
            }

            if (result.LoadedGroups.Count == 0 && result.FailedTypes.Count == 0)
            {
                _ui.TriggerAlert(AlertLevel.Warning, "SET file has no placeable objects");
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
            _ui.InitMissionPanel(result.Name);

            Logger.Info?.PrintMsg(LogClass.Application, $"Loaded {loadedCount} object types with {totalInstances} total instances");
        }
    }
}
