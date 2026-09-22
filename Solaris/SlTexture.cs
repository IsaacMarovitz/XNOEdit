using System.Text;
using Plume;

namespace Solaris
{
    [Flags]
    public enum SlTextureUsage : uint
    {
        None = 0,
        Sampled = 1 << 0,
        ColorTarget = 1 << 1,
        DepthTarget = 1 << 2,
        Storage = 1 << 3,
        Cube = 1 << 4,
    }

    public struct SlTextureDescriptor
    {
        public uint Width = 1;
        public uint Height = 1;
        public uint Depth = 1;
        public uint MipLevels = 1;
        public uint ArraySize = 1;
        public uint SampleCount = 1;
        public RenderFormat Format = RenderFormat.Unknown;
        public RenderTextureDimension Dimension = RenderTextureDimension.Texture2D;
        public RenderComponentMapping ComponentMapping = new RenderComponentMapping();
        public SlTextureUsage Usage = SlTextureUsage.Sampled;

        public SlTextureDescriptor() { }

        public static SlTextureDescriptor Sampled2D(uint width, uint height, RenderFormat format, uint mipLevels = 1) => new()
        {
            Width = width,
            Height = height,
            Format = format,
            MipLevels = mipLevels,
            Usage = SlTextureUsage.Sampled,
        };

        public static SlTextureDescriptor SampledCube(uint size, RenderFormat format, uint mipLevels = 1) => new()
        {
            Dimension = RenderTextureDimension.Texture2D,
            Width = size,
            Height = size,
            Depth = 1,
            MipLevels = mipLevels,
            ArraySize = 6,
            Format = format,
            Usage = SlTextureUsage.Sampled | SlTextureUsage.Cube,
        };

        public static SlTextureDescriptor ColorTarget(uint width, uint height, RenderFormat format, uint sampleCount = 1) => new()
        {
            Width = width,
            Height = height,
            Format = format,
            SampleCount = sampleCount,
            Usage = SlTextureUsage.ColorTarget | SlTextureUsage.Sampled,
        };

        public static SlTextureDescriptor DepthTarget(uint width, uint height, RenderFormat format, uint sampleCount = 1) => new()
        {
            Width = width,
            Height = height,
            Format = format,
            SampleCount = sampleCount,
            Usage = SlTextureUsage.DepthTarget,
        };

        internal RenderTextureFlags ToPlumeFlags()
        {
            var flags = RenderTextureFlags.None;

            if ((Usage & SlTextureUsage.ColorTarget) != 0)
                flags |= RenderTextureFlags.RenderTarget;

            if ((Usage & SlTextureUsage.DepthTarget) != 0)
                flags |= RenderTextureFlags.DepthTarget;

            if ((Usage & SlTextureUsage.Storage) != 0)
                flags |= RenderTextureFlags.Storage;

            if ((Usage & SlTextureUsage.Cube) != 0)
                flags |= RenderTextureFlags.Cube;

            return flags;
        }
    }

    public sealed unsafe class SlTexture : IDisposable
    {
        private RenderTexture* _texture;
        private RenderTextureView* _defaultView;
        private readonly bool _ownsTexture;
        private bool _disposed;

        internal SlTexture(RenderTexture* texture, in SlTextureDescriptor descriptor, bool ownsTexture)
        {
            _texture = texture;
            Descriptor = descriptor;
            _ownsTexture = ownsTexture;
        }

        public SlTextureDescriptor Descriptor { get; }

        public uint Width => Descriptor.Width;

        public uint Height => Descriptor.Height;

        public RenderFormat Format => Descriptor.Format;

        internal RenderTexture* Handle => _texture;

        /// <summary>
        /// A full-subresource view matching the texture's own format and dimension.
        /// Created on first use, since render-target-only textures never need one.
        /// </summary>
        internal RenderTextureView* DefaultView
        {
            get
            {
                if (_defaultView != null)
                    return _defaultView;

                var viewDesc = new RenderTextureViewDesc
                {
                    Format = Descriptor.Format,
                    Dimension = ToViewDimension(Descriptor),
                    MipLevels = Descriptor.MipLevels,
                    ArraySize = Descriptor.ArraySize,
                    ComponentMapping = Descriptor.ComponentMapping,
                };

                _defaultView = _texture->CreateTextureView(&viewDesc);
                return _defaultView;
            }
        }

        internal static RenderTextureViewDimension ToViewDimension(in SlTextureDescriptor descriptor)
        {
            if ((descriptor.Usage & SlTextureUsage.Cube) != 0)
                return RenderTextureViewDimension.TextureCube;

            return descriptor.Dimension switch
            {
                RenderTextureDimension.Texture1D => RenderTextureViewDimension.Texture1D,
                RenderTextureDimension.Texture2D => RenderTextureViewDimension.Texture2D,
                RenderTextureDimension.Texture3D => RenderTextureViewDimension.Texture3D,
                _ => RenderTextureViewDimension.Unknown,
            };
        }

        public void SetName(string name)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            SlUtf8.WithPointer(name, ptr => _texture->SetName(ptr));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_defaultView != null)
            {
                _defaultView->Dispose();
                _defaultView = null;
            }

            // Swap chain textures are owned by the swap chain.
            if (_ownsTexture && _texture != null)
            {
                _texture->Dispose();
            }

            _texture = null;
        }
    }

    internal static unsafe class SlUtf8
    {
        /// <summary>
        /// Converts to a null-terminated UTF-8 buffer valid for the duration of the callback.
        /// Plume borrows name strings for the call only, so a stack buffer is sufficient.
        /// </summary>
        public static void WithPointer(string value, PointerAction action)
        {
            var maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length) + 1;

            if (maxBytes <= 512)
            {
                var buffer = stackalloc byte[maxBytes];
                var written = Encoding.UTF8.GetBytes(value, new Span<byte>(buffer, maxBytes));
                buffer[written] = 0;
                action((sbyte*)buffer);
            }
            else
            {
                var bytes = new byte[maxBytes];
                var written = Encoding.UTF8.GetBytes(value, bytes);
                bytes[written] = 0;

                fixed (byte* buffer = bytes)
                {
                    action((sbyte*)buffer);
                }
            }
        }

        public delegate void PointerAction(sbyte* value);
    }
}
