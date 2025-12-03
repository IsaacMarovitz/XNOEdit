using Marathon.Formats.Archive;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using Marathon.IO.Types.FileSystem;
using Pfim;
using Silk.NET.WebGPU;
using XNOEdit.Logging;
using XNOEdit.Renderer.Renderers;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Services
{
    public enum LoadStage
    {
        Starting,
        Decompressing,
        Parsing,
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

    public record XnoLoadResult(
        NinjaNext Xno,
        ObjectChunk? ObjectChunk,
        ModelRenderer? Renderer,
        List<LoadedTexture> Textures
    );

    public record SetLoadResult(
        StageSet Set
    );

    public record ArcLoadResult(
        string Name,
        List<ArcXnoEntry> Entries,
        List<LoadedTexture> Textures,
        float MaxRadius
    );

    public record ArcXnoEntry(
        NinjaNext Xno,
        ObjectChunk ObjectChunk,
        ModelRenderer Renderer
    );

    public class FileLoaderService
    {
        private readonly WebGPU _wgpu;
        private readonly WgpuDevice _device;
        private readonly IntPtr _queue;

        public FileLoaderService(WebGPU wgpu, WgpuDevice device, IntPtr queue)
        {
            _wgpu = wgpu;
            _device = device;
            _queue = queue;
        }

        public async Task<XnoLoadResult?> ReadXnoAsync(
            IFile file,
            ArcFile? shaderArchive,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
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

                    renderer = new ModelRenderer(_wgpu, _device, objectChunk, textureListChunk, effectChunk, shaderArchive);
                }

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {xno.Name}", 1, 1));

                return new XnoLoadResult(xno, objectChunk, renderer, textures);
            }, cancellationToken);
        }

        public async Task<SetLoadResult?> ReadSetAsync(
            IFile file,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new LoadProgress(LoadStage.Decompressing, $"Decompressing {file.Name}..."));

                var data = file.Decompress();
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new LoadProgress(LoadStage.Parsing, $"Parsing {file.Name}..."));
                var set = new StageSet(data);

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {file.Name}", 1, 1));

                return new SetLoadResult(set);
            }, cancellationToken);
        }

        public async Task<ArcLoadResult?> ReadArcAsync(
            ArcFile file,
            ArcFile? shaderArchive,
            IProgress<LoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entries = new List<ArcXnoEntry>();
                var allTextures = new List<LoadedTexture>();
                var loadedTextureNames = new HashSet<string>();
                var maxRadius = 0f;

                var name = Path.GetFileName(file.Location);
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

                        var renderer = new ModelRenderer(_wgpu, _device, objectChunk, textureListChunk, effectChunk, shaderArchive);

                        // Disable shadow meshes by default
                        if (xno.Name.Contains("sdw"))
                        {
                            renderer.Visible = false;
                        }

                        entries.Add(new ArcXnoEntry(xno, objectChunk, renderer));
                        maxRadius = Math.Max(objectChunk.Radius, maxRadius);
                    }

                    current++;
                }

                progress?.Report(new LoadProgress(LoadStage.Complete, $"Loaded {name}", total, total));

                return new ArcLoadResult(name, entries, allTextures, maxRadius);
            }, cancellationToken);
        }

        private unsafe List<LoadedTexture> LoadTextures(
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

                var pointers = LoadTexture(textureFile);
                if (pointers.texture != IntPtr.Zero)
                    result.Add(new LoadedTexture(textureFile.Name, (Texture*)pointers.texture, (TextureView*)pointers.textureView));
            }

            return result;
        }

        private (IntPtr texture, IntPtr textureView) LoadTexture(IFile file)
        {
            try
            {
                unsafe
                {
                    using var stream = file.Decompress().Open();
                    using var image = Pfimage.FromStream(stream);

                    var mipLevelCount = image.MipMaps != null && image.MipMaps.Length > 0
                        ? (uint)(image.MipMaps.Length + 1)
                        : 1u;

                    Logger.Debug?.PrintMsg(LogClass.Application,
                        $"  Loading {file.Name}: {image.Width}x{image.Height}, {mipLevelCount} mip levels, format: {image.Format}");

                    var textureDesc = new TextureDescriptor
                    {
                        Size = new Extent3D
                        {
                            Width = (uint)image.Width,
                            Height = (uint)image.Height,
                            DepthOrArrayLayers = 1
                        },
                        MipLevelCount = mipLevelCount,
                        SampleCount = 1,
                        Dimension = TextureDimension.Dimension2D,
                        Format = TextureFormat.Bgra8Unorm,
                        Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
                    };

                    var wgpuTexture = _wgpu.DeviceCreateTexture(_device, &textureDesc);

                    UploadMipLevel((IntPtr)wgpuTexture, image.Data, 0, image.Data.Length,
                        image.Width, image.Height, image.Stride, 0, image.Format);

                    if (image.MipMaps is { Length: > 0 })
                    {
                        for (var i = 0; i < image.MipMaps.Length; i++)
                        {
                            var mipMap = image.MipMaps[i];
                            UploadMipLevel((IntPtr)wgpuTexture, image.Data, mipMap.DataOffset, mipMap.DataLen,
                                mipMap.Width, mipMap.Height, mipMap.Stride, (uint)(i + 1), image.Format);
                        }
                    }

                    var viewDesc = new TextureViewDescriptor
                    {
                        Format = TextureFormat.Bgra8Unorm,
                        Dimension = TextureViewDimension.Dimension2D,
                        BaseMipLevel = 0,
                        MipLevelCount = mipLevelCount,
                        BaseArrayLayer = 0,
                        ArrayLayerCount = 1,
                        Aspect = TextureAspect.All
                    };

                    return ((IntPtr)wgpuTexture, (IntPtr)_wgpu.TextureCreateView(wgpuTexture, &viewDesc));
                }
            }
            catch (Exception ex)
            {
                Logger.Error?.PrintMsg(LogClass.Application, $"Failed to load texture {file.Name}: {ex.Message}");
                return (IntPtr.Zero, IntPtr.Zero);
            }
        }

        private unsafe void UploadMipLevel(IntPtr texture, byte[] sourceData, int dataOffset, int dataLen,
            int width, int height, int stride, uint mipLevel, ImageFormat format)
        {
            var mipData = new byte[dataLen];
            Array.Copy(sourceData, dataOffset, mipData, 0, dataLen);

            var imageData = ConvertToRgba(mipData, width, height, stride, format);

            fixed (byte* pData = imageData)
            {
                var imageCopyTexture = new ImageCopyTexture
                {
                    Texture = (Texture*)texture,
                    MipLevel = mipLevel,
                    Origin = new Origin3D { X = 0, Y = 0, Z = 0 },
                    Aspect = TextureAspect.All
                };

                var textureDataLayout = new TextureDataLayout
                {
                    Offset = 0,
                    BytesPerRow = (uint)(width * 4),
                    RowsPerImage = (uint)height
                };

                var writeSize = new Extent3D
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    DepthOrArrayLayers = 1
                };

                _wgpu.QueueWriteTexture((Queue*)_queue, &imageCopyTexture, pData,
                    (nuint)(width * height * 4), &textureDataLayout, &writeSize);
            }
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
