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

            var textureDesc = new TextureDescriptor
            {
                Size = new Extent3D
                {
                    Width = (uint)image.Width,
                    Height = (uint)image.Height,
                    DepthOrArrayLayers = 1
                },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = TextureFormat.Bgra8Unorm,
                Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
            };

            var wgpuTexture = _wgpu.DeviceCreateTexture(_device, &textureDesc);

            var imageData = ConvertToRgba(image);
            UploadTextureData(wgpuTexture, imageData, image.Width, image.Height);

            var textureView = CreateTextureView(wgpuTexture);

            _controller.BindImGuiTextureView(textureView);
            _textures.Add(name, (IntPtr)textureView);
        }

        private static byte[] ConvertToRgba(IImage image)
        {
            var imageData = image.Data;

            if (image.Format == ImageFormat.Rgb24)
            {
                var rgbaData = new byte[image.Width * image.Height * 4];
                for (int i = 0, j = 0; i < imageData.Length; i += 3, j += 4)
                {
                    rgbaData[j] = imageData[i];         // R
                    rgbaData[j + 1] = imageData[i + 1]; // G
                    rgbaData[j + 2] = imageData[i + 2]; // B
                    rgbaData[j + 3] = 255;              // A
                }
                return rgbaData;
            }

            return imageData;
        }

        private void UploadTextureData(Texture* texture, byte[] data, int width, int height)
        {
            fixed (byte* pData = data)
            {
                var imageCopyTexture = new ImageCopyTexture
                {
                    Texture = texture,
                    MipLevel = 0,
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

        private TextureView* CreateTextureView(Texture* texture)
        {
            var viewDesc = new TextureViewDescriptor
            {
                Format = TextureFormat.Bgra8Unorm,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            return _wgpu.TextureCreateView(texture, &viewDesc);
        }

        public void ClearTextures()
        {
            foreach (var texturePtr in _textures.Values)
            {
                var textureView = (TextureView*)texturePtr;
                _controller.UnbindImGuiTextureView(textureView);
                _wgpu.TextureViewRelease(textureView);
            }

            _textures.Clear();
        }

        public void Dispose()
        {
            ClearTextures();
        }
    }
}
