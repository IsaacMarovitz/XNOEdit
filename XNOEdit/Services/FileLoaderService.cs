using System.Buffers.Binary;
using System.Numerics;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using Marathon.IO.Types.FileSystem;
using Pfim;
using Plume;
using Solaris;
using XNOEdit.Guest;
using XNOEdit.Logging;
using XNOEdit.ModelResolver;
using XNOEdit.Renderer.Renderers;

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
        SceneConfig? SceneConfig,
        float MaxRadius
    );

    public record ArcXnoEntry(
        NinjaNext Xno,
        ObjectChunk ObjectChunk,
        ModelRenderer Renderer
    );

    public class FileLoaderService
    {
        private readonly SlDevice _device;
        private readonly SlUploader _uploader;

        public FileLoaderService(SlDevice device, SlUploader uploader)
        {
            _device = device;
            _uploader = uploader;
        }

        public async Task<ObjectLoadResult?> ReadXnoAsync(
            IFile file,
            GuestMaterialCache? guestMaterials,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Logger.Info?.PrintMsg(LogClass.Application, $"Loading XNO: {file.Name}");

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.Decompressing, $"Decompressing {file.Name}..."));

                var data = file.Decompress();
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new LoadProgress(LoadStage.Parsing, $"Parsing {file.Name}..."));
                var xno = new NinjaNext(data);

                var objectChunk = xno.GetChunk<ObjectChunk>();
                var effectChunk = xno.GetChunk<EffectListChunk>();
                var textureListChunk = xno.GetChunk<TextureListChunk>();

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.LoadingTextures, "Loading textures..."));

                var textures = LoadTextures(file, textureListChunk, cancellationToken);

                ModelRenderer? renderer = null;
                if (objectChunk != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new LoadProgress(LoadStage.CreatingBuffers, "Creating GPU buffers..."));

                    renderer = new ModelRenderer(_device, objectChunk, textureListChunk, effectChunk, guestMaterials);
                }

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {xno.Name}", 1, 1));

                return new ObjectLoadResult(xno, objectChunk, renderer, textures);
            }, cancellationToken);
        }

        public async Task<MissionLoadResult?> ReadMissionAsync(
            IFile file,
            ResolverContext resolverContext,
            GuestMaterialCache? guestMaterials,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(async () =>
            {
                Logger.Info?.PrintMsg(LogClass.Application, $"Loading Mission: {file.Name}");

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.Decompressing, $"Decompressing {file.Name}..."));

                var data = file.Decompress();
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new LoadProgress(LoadStage.Parsing, $"Parsing {file.Name}..."));
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

                    var xnoResult = await ReadXnoAsync(modelFile, guestMaterials, null, cancellationToken);
                    if (xnoResult?.ObjectChunk != null)
                    {
                        loadedGroups.Add(new LoadedObjectGroup(
                            modelKey.ModelPath,
                            xnoResult,
                            instancesByModel[modelKey]));
                    }

                    current++;
                }

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {file.Name}", total, total));

                return new MissionLoadResult(file.Name, set, loadedGroups, failedTypes);
            }, cancellationToken);
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
                Logger.Info?.PrintMsg(LogClass.Application, $"Loading ARC: {name}");

                progress?.Report(new LoadProgress(LoadStage.Starting, $"Scanning {name}..."));

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

                return new StageLoadResult(name, entries, allTextures, config, maxRadius);
            }, cancellationToken);
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

                private SlTexture? LoadTexture(IFile file)
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

                using var image = Pfimage.FromStream(new MemoryStream(bytes));

                var mipLevelCount = image.MipMaps != null && image.MipMaps.Length > 0
                    ? (uint)(image.MipMaps.Length + 1)
                    : 1u;

                Logger.Debug?.PrintMsg(LogClass.Application,
                    $"  Loading {file.Name}: {image.Width}x{image.Height}, {mipLevelCount} mip levels, format: {image.Format}");

                var single = image.Format == ImageFormat.Rgb8;

                var descriptor = SlTextureDescriptor.Sampled2D(
                    (uint)image.Width, (uint)image.Height,
                    single ? RenderFormat.R8Unorm : RenderFormat.B8G8R8A8Unorm,
                    mipLevelCount);

                if (single)
                    descriptor = descriptor with { ComponentMapping = SingleChannelMapping(bytes) };

                var texture = _device.CreateTexture(descriptor, file.Name);

                UploadMipLevel(texture, image.Data, 0, image.Data.Length,
                    image.Width, image.Height, image.Stride, 0, image.Format);

                if (image.MipMaps is { Length: > 0 })
                {
                    for (var i = 0; i < image.MipMaps.Length; i++)
                    {
                        var mipMap = image.MipMaps[i];
                        UploadMipLevel(texture, image.Data, mipMap.DataOffset, mipMap.DataLen,
                            mipMap.Width, mipMap.Height, mipMap.Stride, (uint)(i + 1), image.Format);
                    }
                }

                return texture;
            }
            catch (Exception ex)
            {
                Logger.Error?.PrintMsg(LogClass.Application, $"Failed to load texture {file.Name}: {ex.Message}");
                return null;
            }
        }

        private static RenderComponentMapping SingleChannelMapping(ReadOnlySpan<byte> dds)
        {
            const uint DdpfAlpha = 0x2;
            const uint DdpfLuminance = 0x20000;

            var flags = dds.Length >= 84
                ? BinaryPrimitives.ReadUInt32LittleEndian(dds[80..84])
                : 0u;

            if ((flags & DdpfAlpha) != 0)
            {
                return new RenderComponentMapping(
                    RenderSwizzle.Zero, RenderSwizzle.Zero, RenderSwizzle.Zero, RenderSwizzle.R);
            }

            if ((flags & DdpfLuminance) != 0)
            {
                return new RenderComponentMapping(
                    RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.One);
            }

            Logger.Warning?.PrintMsg(LogClass.Application,
                $"Single-channel DDS with neither the alpha nor luminance flag (0x{flags:X})");

            return new RenderComponentMapping(
                RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.One);
        }

        private void UploadMipLevel(SlTexture texture, byte[] sourceData, int dataOffset, int dataLen,
            int width, int height, int stride, uint mipLevel, ImageFormat format)
        {
            var mipData = new byte[dataLen];
            Array.Copy(sourceData, dataOffset, mipData, 0, dataLen);

            var single = format == ImageFormat.Rgb8;

            var imageData = single
                ? PackSingleChannel(mipData, width, height, stride)
                : ConvertToRgba(mipData, width, height, stride, format);

            _uploader.StageTexture(
                texture,
                imageData,
                (uint)width,
                (uint)height,
                bytesPerRow: (uint)(width * (single ? 1 : 4)),
                mipLevel: mipLevel);
        }

        private static byte[] PackSingleChannel(byte[] data, int width, int height, int stride)
        {
            var packed = new byte[width * height];

            for (var y = 0; y < height; y++)
            {
                Array.Copy(data, y * stride, packed, y * width, width);
            }

            return packed;
        }

        private static byte[] ConvertToRgba(byte[] data, int width, int height, int stride, ImageFormat format)
        {
            var bytesPerPixel = GetBytesPerPixel(format);
            var rgbaData = new byte[width * height * 4];

            for (var y = 0; y < height; y++)
            {
                var srcRowStart = y * stride;
                var dstRowStart = y * width * 4;

                for (var x = 0; x < width; x++)
                {
                    var srcIdx = srcRowStart + x * bytesPerPixel;
                    var dstIdx = dstRowStart + x * 4;

                    switch (format)
                    {
                        case ImageFormat.Rgb24:
                            rgbaData[dstIdx + 0] = data[srcIdx + 0];
                            rgbaData[dstIdx + 1] = data[srcIdx + 1];
                            rgbaData[dstIdx + 2] = data[srcIdx + 2];
                            rgbaData[dstIdx + 3] = 255;
                            break;

                        case ImageFormat.Rgba32:
                            rgbaData[dstIdx + 0] = data[srcIdx + 0];
                            rgbaData[dstIdx + 1] = data[srcIdx + 1];
                            rgbaData[dstIdx + 2] = data[srcIdx + 2];
                            rgbaData[dstIdx + 3] = data[srcIdx + 3];
                            break;

                        case ImageFormat.Rgb8:
                            var gray = data[srcIdx];
                            rgbaData[dstIdx + 0] = gray;
                            rgbaData[dstIdx + 1] = gray;
                            rgbaData[dstIdx + 2] = gray;
                            rgbaData[dstIdx + 3] = 255;
                            break;

                        default:
                            if (bytesPerPixel >= 4)
                            {
                                rgbaData[dstIdx + 0] = data[srcIdx + 0];
                                rgbaData[dstIdx + 1] = data[srcIdx + 1];
                                rgbaData[dstIdx + 2] = data[srcIdx + 2];
                                rgbaData[dstIdx + 3] = data[srcIdx + 3];
                            }
                            break;
                    }
                }
            }

            return rgbaData;
        }

        private static int GetBytesPerPixel(ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Rgb8 => 1,
                ImageFormat.Rgb24 => 3,
                _ => 4
            };
        }
    }
}
