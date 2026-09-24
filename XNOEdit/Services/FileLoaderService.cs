using System.Numerics;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using Marathon.Formats.Text;
using Marathon.IO.Types.FileSystem;
using Plume;
using Solaris;
using XNOEdit.Formats;
using XNOEdit.Guest;
using XNOEdit.Logging;
using XNOEdit.ModelResolver;
using XNOEdit.Panels;
using XNOEdit.Render;
using XNOEdit.Render.Renderers;

namespace XNOEdit.Services
{
    public enum LoadStage
    {
        Starting,
        Decompressing,
        Parsing,
        Resolving,
        LoadingTextures,
        CreatingBuffers,
        LoadingModel,
        Complete
    }

    public record LoadProgress(
        LoadStage Stage,
        string Message,
        int Current = 0,
        int Total = 0
    )
    {
        public float Percentage => Total > 0 ? (float)Current / Total : 0f;
        public bool IsIndeterminate => Total == 0;
    }

    public record ObjectLoadResult(
        NinjaNext Xno,
        MaterialMotionChunk? MaterialMotion,
        ObjectChunk? ObjectChunk,
        ModelRenderer? Renderer,
        List<LoadedTexture> Textures
    );

    public record MissionLoadResult(
        string Name,
        StageSet Set,
        List<LoadedObjectGroup> LoadedGroups,
        HashSet<string> FailedTypes
    );

    public record LoadedObjectGroup(
        string ModelPath,
        ObjectLoadResult ObjectResult,
        List<ResolvedInstanceData> Instances
    );

    public record ResolvedInstanceData(
        Vector3 Position,
        Quaternion Rotation
    );

    public record ModelKey(
        string ModelPath,
        string? ArchiveHint
    );

    public record StageLoadResult(
        string Name,
        List<ArcXnoEntry> Entries,
        List<LoadedTexture> Textures,
        LoadedTexture? EnvMap,
        SceneConfig? SceneConfig,
        float MaxRadius
    );

    public record EnvironmentLoadResult(
        SceneConfig Config,
        LoadedTexture? EnvMap
    );

    public record GameFontLoadResult(
        GameFont Font,
        LoadedTexture Atlas
    );

    public record ArcXnoEntry(
        NinjaNext Xno,
        ObjectChunk ObjectChunk,
        ModelRenderer Renderer
    );

    public class FileLoaderService
    {
        private static readonly RenderComponentMapping WhiteCoverage = new(
            RenderSwizzle.One, RenderSwizzle.One, RenderSwizzle.One, RenderSwizzle.R);

        private readonly SlDevice _device;
        private readonly SlUploader _uploader;

        public FileLoaderService(SlDevice device, SlUploader uploader)
        {
            _device = device;
            _uploader = uploader;
        }

        public async Task<ObjectLoadResult?> ReadXnoAsync(
            FileEntry entry,
            GuestMaterialCache? guestMaterials,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info?.PrintMsg(LogClass.Application, $"Loading XNO: {entry.File.Name}");

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.Decompressing, $"Decompressing {entry.File.Name}..."));

                var data = entry.File.Decompress();
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new LoadProgress(LoadStage.Parsing, $"Parsing {entry.File.Name}..."));
                var xno = new NinjaNext(data);

                var materialMotionPath = entry.File.Path.Replace(".xno", ".xnv");
                var materialMotionFile = entry.ArcFile.GetFile(materialMotionPath);
                MaterialMotionChunk? materialMotion = null;

                if (materialMotionFile != null)
                {
                    progress?.Report(new LoadProgress(LoadStage.LoadingTextures, $"Parsing {entry.File.Name} material motion..."));
                    materialMotion = new NinjaNext(materialMotionFile.Decompress()).GetChunk<MaterialMotionChunk>();
                }

                var objectChunk = xno.GetChunk<ObjectChunk>();
                var effectChunk = xno.GetChunk<EffectListChunk>();
                var textureListChunk = xno.GetChunk<TextureListChunk>();

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.LoadingTextures, "Loading textures..."));

                var textures = LoadTextures(entry.File, textureListChunk, cancellationToken);

                ModelRenderer? renderer = null;
                if (objectChunk != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new LoadProgress(LoadStage.CreatingBuffers, "Creating GPU buffers..."));

                    renderer = new ModelRenderer(_device, objectChunk, textureListChunk, effectChunk, guestMaterials);
                }

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {xno.Name}", 1, 1));

                return new ObjectLoadResult(xno, materialMotion, objectChunk, renderer, textures);
            }, cancellationToken);
        }

        public async Task<MissionLoadResult?> ReadMissionAsync(
            FileEntry entry,
            ResolverContext resolverContext,
            GuestMaterialCache? guestMaterials,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(async () =>
            {
                Logger.Info?.PrintMsg(LogClass.Application, $"Loading Mission: {entry.File.Name}");

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.Decompressing, $"Decompressing {entry.File.Name}..."));

                var data = entry.File.Decompress();
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new LoadProgress(LoadStage.Parsing, $"Parsing {entry.File.Name}..."));
                var set = new StageSet(data);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.Resolving, "Resolving objects..."));

                var (instancesByModel, failedTypes) = ResolveObjects(set, resolverContext);

                // Load XNOs for each unique model
                var loadedGroups = new List<LoadedObjectGroup>();
                var modelKeys = instancesByModel.Keys.ToList();
                var current = 0;
                var total = modelKeys.Count;

                var archiveCache = new Dictionary<string, ArcFile>();

                foreach (var modelKey in modelKeys)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new LoadProgress(
                        LoadStage.LoadingModel,
                        $"Loading {Path.GetFileName(modelKey.ModelPath)}...",
                        current,
                        total));

                    var modelFile = GetModelFile(modelKey, resolverContext.ObjectArchive, archiveCache);
                    if (modelFile == null)
                    {
                        Logger.Warning?.PrintMsg(LogClass.Application, $"Model not found: {modelKey.ModelPath}");
                        current++;
                        continue;
                    }

                    var xnoResult = await ReadXnoAsync(new FileEntry(modelFile, resolverContext.ObjectArchive), guestMaterials, null, cancellationToken);
                    if (xnoResult?.ObjectChunk != null)
                    {
                        loadedGroups.Add(new LoadedObjectGroup(
                            modelKey.ModelPath,
                            xnoResult,
                            instancesByModel[modelKey]));
                    }

                    current++;
                }

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {entry.File.Name}", total, total));

                return new MissionLoadResult(entry.File.Name, set, loadedGroups, failedTypes);
            }, cancellationToken);
        }

        public Task<EnvironmentLoadResult?> ReadEnvironmentAsync(string stageName)
        {
            return Task.Run<EnvironmentLoadResult?>(async () =>
            {
                if (await StageConfigsMap.GetSceneConfig(stageName) is not { } config)
                    return null;

                return new EnvironmentLoadResult(config, LoadEnvMap(ArcFiles.Win32Arc(stageName), config));
            });
        }

        private static (Dictionary<ModelKey, List<ResolvedInstanceData>>, HashSet<string>) ResolveObjects(
            StageSet set,
            ResolverContext context)
        {
            var instancesByModel = new Dictionary<ModelKey, List<ResolvedInstanceData>>();
            var failedTypes = new HashSet<string>();
            var registry = new ResolverRegistry();

            foreach (var setObject in set.Objects)
            {
                var match = registry.Resolve(context, setObject);

                if (match.Skip)
                    continue;

                if (match.Success)
                {
                    foreach (var instance in match.Instances)
                    {
                        var modelPath = NormalizeModelPath(instance.ModelPath);
                        var key = new ModelKey(modelPath, instance.ArchiveHint);

                        if (!instancesByModel.TryGetValue(key, out var instances))
                        {
                            instances = [];
                            instancesByModel[key] = instances;
                        }

                        instances.Add(new ResolvedInstanceData(instance.Position, instance.Rotation));
                    }
                }
                else
                {
                    if (match.ErrorMessage != null && !failedTypes.Contains(setObject.Type))
                    {
                        Logger.Warning?.PrintMsg(LogClass.Application, match.ErrorMessage);
                        failedTypes.Add(setObject.Type);
                    }
                }
            }

            return (instancesByModel, failedTypes);
        }

        private static string NormalizeModelPath(string path)
        {
            if (!Path.HasExtension(path))
                return path + ".xno";
            return path;
        }

        private static IFile? GetModelFile(
            ModelKey modelKey,
            ArcFile objectArchive,
            Dictionary<string, ArcFile> archiveCache)
        {
            if (modelKey.ArchiveHint == null)
            {
                // Default: look in object.arc
                return objectArchive.GetFile($"{modelKey.ModelPath}");
            }

            // Look in the hinted archive
            if (!archiveCache.TryGetValue(modelKey.ArchiveHint, out var archive))
            {
                var archivePath = Path.Join(
                    Configuration.GameFolder,
                    $"{modelKey.ArchiveHint}.arc");

                if (!File.Exists(archivePath))
                {
                    Logger.Warning?.PrintMsg(LogClass.Application, $"Archive not found: {archivePath}");
                    return null;
                }

                archive = new ArcFile(archivePath);
                archiveCache[modelKey.ArchiveHint] = archive;
            }

            return archive.GetFile($"{modelKey.ModelPath}");
        }

        public async Task<StageLoadResult?> ReadArcAsync(
            ArcFile file,
            GuestMaterialCache? guestMaterials,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var configTask = StageConfigsMap.GetSceneConfig(file.Name.Replace(".arc", ""));
            var config = await configTask;

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entries = new List<ArcXnoEntry>();
                var allTextures = new List<LoadedTexture>();
                var loadedTextureNames = new HashSet<string>();
                var maxRadius = 0f;

                var name = Path.GetFileNameWithoutExtension(file.Location);
                Logger.Info?.PrintMsg(LogClass.Application, $"Loading ARC: {file.Location}");

                progress?.Report(new LoadProgress(LoadStage.Starting, $"Scanning {name}..."));

                var envMap = config is { } sceneConfig ? LoadEnvMap(file, sceneConfig) : null;
                var models = file.EnumerateFiles("*.xno", SearchOption.AllDirectories).ToList();
                var total = models.Count;
                var current = 0;

                foreach (var model in models)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(new LoadProgress(
                        LoadStage.LoadingModel,
                        $"Loading {model.Name}...",
                        current,
                        total
                    ));

                    var xno = new NinjaNext(model.Decompress());
                    Logger.Debug?.PrintMsg(LogClass.Application, $"Loading XNO: {xno.Name}");

                    var objectChunk = xno.GetChunk<ObjectChunk>();
                    var effectChunk = xno.GetChunk<EffectListChunk>();
                    var textureListChunk = xno.GetChunk<TextureListChunk>();

                    if (objectChunk != null)
                    {
                        // Load textures (skip already loaded ones)
                        var textures = LoadTextures(model, textureListChunk, cancellationToken, loadedTextureNames);
                        allTextures.AddRange(textures);
                        foreach (var tex in textures)
                            loadedTextureNames.Add(tex.Name);

                        var renderer = new ModelRenderer(_device, objectChunk, textureListChunk, effectChunk, guestMaterials);

                        // Disable shadow meshes by default
                        if (xno.Name.Contains("sdw"))
                        {
                            renderer.SetVisible(false);
                        }

                        entries.Add(new ArcXnoEntry(xno, objectChunk, renderer));
                        maxRadius = Math.Max(objectChunk.Radius, maxRadius);
                    }

                    current++;
                }

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {name}", total, total));

                return new StageLoadResult(name, entries, allTextures, envMap, config, maxRadius);
            }, cancellationToken);
        }

        private LoadedTexture? LoadEnvMap(ArcFile archive, SceneConfig config)
        {
            if (string.IsNullOrEmpty(config.EnvMap))
                return null;

            if (archive.GetFile(Path.Join("win32", config.EnvMap)) is not { } file)
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"Env map not found: {config.EnvMap}");
                return null;
            }

            return LoadTexture(file) is { } texture ? new LoadedTexture(config.EnvMap, texture) : null;
        }

        private List<LoadedTexture> LoadTextures(
            IFile file,
            TextureListChunk? textureListChunk,
            CancellationToken cancellationToken,
            HashSet<string>? skipNames = null)
        {
            var result = new List<LoadedTexture>();

            if (textureListChunk == null)
                return result;

            var parentDirectory = file.Parent;
            foreach (var textureFile in parentDirectory.EnumerateFiles("*.dds", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (textureListChunk.Textures.All(x => x.Name != textureFile.Name))
                    continue;

                if (skipNames != null && skipNames.Contains(textureFile.Name))
                    continue;

                var texture = LoadTexture(textureFile);
                if (texture != null)
                    result.Add(new LoadedTexture(textureFile.Name, texture));
            }

            return result;
        }

        public List<GameFontLoadResult> ReadGameFonts()
        {
            var archive = ArcFiles.TextArc;
            var result = new List<GameFontLoadResult>();

            foreach (var data in GameFont.TextFonts)
            {
                if (ReadGameFont(archive, data) is { } font)
                    result.Add(font);
            }

            return result;
        }

        private GameFontLoadResult? ReadGameFont(ArcFile archive, GameFont.TextFontData data)
        {
            if (archive.GetFile(Path.Combine("xenon", data.MapPath)) is not { } mapFile ||
                archive.GetFile(Path.Combine("common", data.ProportionPath)) is not { } proportionFile ||
                archive.GetFile(Path.Combine("win32", data.TexturePath)) is not { } atlasFile)
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"Text font not found in text.arc: {data.MapPath}");
                return null;
            }

            if (LoadTexture(atlasFile, WhiteCoverage) is not { } atlas)
                return null;

            var map = new TextFontMap(mapFile.Decompress());
            var proportion = new TextFontProportion(proportionFile.Decompress());

            var font = new GameFont(
                map, proportion,
                new Vector2(data.CellWidth, data.CellHeight),
                new Vector2(data.TextureWidth, data.TextureHeight),
                atlasFile.Name);

            return new GameFontLoadResult(font, new LoadedTexture(atlasFile.Name, atlas));
        }

        private SlTexture? LoadTexture(IFile file, RenderComponentMapping? mapping = null)
        {
            try
            {
                byte[] bytes;

                using (var stream = file.Decompress().Open())
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }

                if (DdsImage.Parse(bytes, file.Name) is not { } dds)
                    return null;

                Logger.Debug?.PrintMsg(LogClass.Application,
                    $"  Loading {file.Name}: {dds.Width}x{dds.Height}, {dds.MipLevels} mip levels, " +
                    $"{dds.Faces} face(s), {dds.Format}");

                var descriptor = dds.IsCubeMap
                    ? SlTextureDescriptor.SampledCube((uint)dds.Width, dds.Format, (uint)dds.MipLevels)
                    : SlTextureDescriptor.Sampled2D((uint)dds.Width, (uint)dds.Height, dds.Format, (uint)dds.MipLevels);

                var texture = _device.CreateTexture(descriptor with { ComponentMapping = mapping ?? dds.Mapping }, file.Name);

                for (var face = 0; face < dds.Faces; face++)
                {
                    for (var level = 0; level < dds.MipLevels; level++)
                    {
                        var (width, height) = dds.LevelSize(level);

                        _uploader.StageTexture(
                            texture,
                            dds.Data.AsSpan(dds.Offset(face, level), dds.LevelLength(level)),
                            (uint)width,
                            (uint)height,
                            bytesPerRow: (uint)dds.RowPitch(level),
                            mipLevel: (uint)level,
                            arrayIndex: (uint)face);
                    }
                }

                return texture;
            }
            catch (Exception ex)
            {
                Logger.Error?.PrintMsg(LogClass.Application,
                    $"Failed to load texture {file.Name}: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
