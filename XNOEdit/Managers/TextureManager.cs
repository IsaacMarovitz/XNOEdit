using Marathon.Formats.Ninja.Chunks;
using Pfim;
using Silk.NET.WebGPU;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Managers
{
    public unsafe class TextureManager : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly WgpuDevice _device;
        private readonly Queue* _queue;
        private readonly ImGuiController _controller;
        private readonly Dictionary<string, IntPtr> _textures = [];

        public IReadOnlyDictionary<string, IntPtr> Textures => _textures;

        public TextureManager(WebGPU wgpu, WgpuDevice device, Queue* queue, ImGuiController controller)
        {
            _wgpu = wgpu;
            _device = device;
            _queue = queue;
            _controller = controller;
        }

        public void LoadTextures(string baseDirectory, TextureListChunk textureListChunk)
        {
            ClearTextures();

            if (textureListChunk == null) return;

            Console.WriteLine($"Loading {textureListChunk.Textures.Count} textures...");

            foreach (var texture in textureListChunk.Textures)
            {
                var texturePath = Path.Combine(baseDirectory, texture.Name);

                if (!File.Exists(texturePath))
                {
                    Console.WriteLine($"  Warning: Texture not found: {texture.Name}");
                    continue;
                }

                LoadTexture(texture.Name, texturePath);
            }
        }

        private void LoadTexture(string name, string path)
        {
            using var image = Pfimage.FromFile(path);

            var mipLevelCount = image.MipMaps != null && image.MipMaps.Length > 0
                ? (uint)(image.MipMaps.Length + 1)
                : 1u;

            Console.WriteLine($"  Loading {name}: {image.Width}x{image.Height}, {mipLevelCount} mip levels, format: {image.Format}");

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

            UploadMipLevel(wgpuTexture, image.Data, 0, image.Data.Length,
                          image.Width, image.Height, image.Stride, 0, image.Format);

            if (image.MipMaps != null && image.MipMaps.Length > 0)
            {
                for (int i = 0; i < image.MipMaps.Length; i++)
                {
                    var mipMap = image.MipMaps[i];

                    UploadMipLevel(wgpuTexture, image.Data, mipMap.DataOffset, mipMap.DataLen,
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

            var textureView = _wgpu.TextureCreateView(wgpuTexture, &viewDesc);
            _controller.BindImGuiTextureView(textureView);

            _textures.TryAdd(name, (IntPtr)textureView);
        }

        private void UploadMipLevel(Texture* texture, byte[] sourceData, int dataOffset, int dataLen,
                                    int width, int height, int stride, uint mipLevel, ImageFormat format)
        {
            var mipData = new byte[dataLen];
            Array.Copy(sourceData, dataOffset, mipData, 0, dataLen);

            var imageData = ConvertToRgba(mipData, width, height, stride, format);

            fixed (byte* pData = imageData)
            {
                var imageCopyTexture = new ImageCopyTexture
                {
                    Texture = texture,
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

                _wgpu.QueueWriteTexture(_queue, &imageCopyTexture, pData,
                    (nuint)(width * height * 4), &textureDataLayout, &writeSize);
            }
        }

        private static byte[] ConvertToRgba(byte[] data, int width, int height, int stride, ImageFormat format)
        {
            var bytesPerPixel = GetBytesPerPixel(format);
            var rgbaData = new byte[width * height * 4];

            // Handle stride (row padding)
            for (int y = 0; y < height; y++)
            {
                int srcRowStart = y * stride;
                int dstRowStart = y * width * 4;

                for (int x = 0; x < width; x++)
                {
                    int srcIdx = srcRowStart + x * bytesPerPixel;
                    int dstIdx = dstRowStart + x * 4;

                    switch (format)
                    {
                        case ImageFormat.Rgb24:
                            rgbaData[dstIdx + 0] = data[srcIdx + 0]; // R
                            rgbaData[dstIdx + 1] = data[srcIdx + 1]; // G
                            rgbaData[dstIdx + 2] = data[srcIdx + 2]; // B
                            rgbaData[dstIdx + 3] = 255;              // A
                            break;

                        case ImageFormat.Rgba32:
                            rgbaData[dstIdx + 0] = data[srcIdx + 0]; // R
                            rgbaData[dstIdx + 1] = data[srcIdx + 1]; // G
                            rgbaData[dstIdx + 2] = data[srcIdx + 2]; // B
                            rgbaData[dstIdx + 3] = data[srcIdx + 3]; // A
                            break;

                        case ImageFormat.Rgb8:
                            // 8-bit indexed color - copy as grayscale
                            var gray = data[srcIdx];
                            rgbaData[dstIdx + 0] = gray;
                            rgbaData[dstIdx + 1] = gray;
                            rgbaData[dstIdx + 2] = gray;
                            rgbaData[dstIdx + 3] = 255;
                            break;

                        default:
                            // For other formats, assume it's already in RGBA order
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

        public void ClearTextures()
        {
            foreach (var texturePtr in _textures.Values)
            {
                _controller.UnbindImGuiTextureView((TextureView*)texturePtr);
                _wgpu.TextureViewRelease((TextureView*)texturePtr);
            }
            _textures.Clear();
        }

        public void Dispose()
        {
            ClearTextures();
        }
    }
}
