using System.Buffers.Binary;
using Plume;
using XNOEdit.Logging;

namespace XNOEdit.Formats
{
    public sealed class DdsImage
    {
        private const int Magic = 0x20534444; // "DDS "
        private const int HeaderSize = 128;   // magic + 124-byte header

        private const uint PixelFormatAlphaPixels = 0x1;
        private const uint PixelFormatAlpha = 0x2;
        private const uint PixelFormatFourCc = 0x4;
        private const uint PixelFormatRgb = 0x40;
        private const uint PixelFormatLuminance = 0x20000;

        private const uint Caps2CubeMap = 0x200;

        private DdsImage(
            RenderFormat format,
            RenderComponentMapping mapping,
            int width,
            int height,
            int mipLevels,
            int faces,
            int blockSize,
            int blockBytes,
            int bytesPerPixel,
            byte[] data)
        {
            Format = format;
            Mapping = mapping;
            Width = width;
            Height = height;
            MipLevels = mipLevels;
            Faces = faces;
            Data = data;

            _blockSize = blockSize;
            _blockBytes = blockBytes;
            _bytesPerPixel = bytesPerPixel;
        }

        private readonly int _blockSize;
        private readonly int _blockBytes;
        private readonly int _bytesPerPixel;

        public RenderFormat Format { get; }

        public RenderComponentMapping Mapping { get; }

        public int Width { get; }

        public int Height { get; }

        public int MipLevels { get; }

        public int Faces { get; }

        public byte[] Data { get; }

        public bool IsCubeMap => Faces == 6;

        public static DdsImage? Parse(byte[] file, string name)
        {
            if (file.Length < HeaderSize)
            {
                Logger.Error?.PrintMsg(LogClass.Application, $"{name}: too short to be a DDS");
                return null;
            }

            var header = file.AsSpan(0, HeaderSize);

            if (BinaryPrimitives.ReadInt32LittleEndian(header) != Magic)
            {
                Logger.Error?.PrintMsg(LogClass.Application, $"{name}: not a DDS");
                return null;
            }

            var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
            var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
            var mipLevels = Math.Max(1, (int)BinaryPrimitives.ReadUInt32LittleEndian(header[28..]));

            var pixelFlags = BinaryPrimitives.ReadUInt32LittleEndian(header[80..]);
            var fourCc = BinaryPrimitives.ReadUInt32LittleEndian(header[84..]);
            var bitCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[88..]);
            var caps2 = BinaryPrimitives.ReadUInt32LittleEndian(header[112..]);

            var faces = (caps2 & Caps2CubeMap) != 0 ? 6 : 1;

            if (!Describe(name, pixelFlags, fourCc, bitCount,
                    out var format, out var mapping, out var blockSize, out var blockBytes, out var bytesPerPixel))
            {
                return null;
            }

            var payload = file.AsSpan(HeaderSize).ToArray();

            var image = new DdsImage(
                format, mapping, width, height, mipLevels, faces,
                blockSize, blockBytes, bytesPerPixel, payload);

            var expected = image.FaceLength() * faces;

            if (payload.Length < expected)
            {
                Logger.Error?.PrintMsg(LogClass.Application,
                    $"{name}: {format} {width}x{height} with {mipLevels} mip(s) and {faces} face(s) " +
                    $"needs {expected} bytes, payload is {payload.Length}");

                return null;
            }

            return image;
        }

        public int Offset(int face, int level)
        {
            var offset = face * FaceLength();

            for (var i = 0; i < level; i++)
            {
                offset += LevelLength(i);
            }

            return offset;
        }

        public int LevelLength(int level)
        {
            var (width, height) = LevelSize(level);

            return _blockSize > 1
                ? BlocksAcross(width) * BlocksAcross(height) * _blockBytes
                : width * height * _bytesPerPixel;
        }

        public int RowPitch(int level)
        {
            var (width, _) = LevelSize(level);

            return _blockSize > 1
                ? BlocksAcross(width) * _blockBytes
                : width * _bytesPerPixel;
        }

        public (int Width, int Height) LevelSize(int level) =>
            (Math.Max(1, Width >> level), Math.Max(1, Height >> level));

        private int FaceLength()
        {
            var total = 0;

            for (var level = 0; level < MipLevels; level++)
            {
                total += LevelLength(level);
            }

            return total;
        }

        private int BlocksAcross(int pixels) => (pixels + _blockSize - 1) / _blockSize;

        private static bool Describe(
            string name,
            uint pixelFlags,
            uint fourCc,
            int bitCount,
            out RenderFormat format,
            out RenderComponentMapping mapping,
            out int blockSize,
            out int blockBytes,
            out int bytesPerPixel)
        {
            mapping = new RenderComponentMapping();
            blockSize = 1;
            blockBytes = 0;
            bytesPerPixel = 0;

            if ((pixelFlags & PixelFormatFourCc) != 0)
            {
                blockSize = 4;

                switch (fourCc)
                {
                    case 0x31545844: format = RenderFormat.Bc1Unorm; blockBytes = 8; return true;  // DXT1
                    case 0x32545844:                                                               // DXT2
                    case 0x33545844: format = RenderFormat.Bc2Unorm; blockBytes = 16; return true; // DXT3
                    case 0x34545844:                                                               // DXT4
                    case 0x35545844: format = RenderFormat.Bc3Unorm; blockBytes = 16; return true; // DXT5
                    case 0x31495441:                                                               // ATI1
                    case 0x55344342: format = RenderFormat.Bc4Unorm; blockBytes = 8; return true;  // BC4U
                    case 0x32495441:                                                               // ATI2
                    case 0x55354342: format = RenderFormat.Bc5Unorm; blockBytes = 16; return true; // BC5U

                    default:
                        Logger.Error?.PrintMsg(LogClass.Application,
                            $"{name}: unhandled fourCC '{FourCcText(fourCc)}'");

                        format = RenderFormat.Unknown;
                        return false;
                }
            }

            if ((pixelFlags & PixelFormatRgb) != 0 && bitCount == 32)
            {
                format = RenderFormat.B8G8R8A8Unorm;
                bytesPerPixel = 4;
                return true;
            }

            if (bitCount == 8)
            {
                bytesPerPixel = 1;
                format = RenderFormat.R8Unorm;

                if ((pixelFlags & PixelFormatAlpha) != 0)
                {
                    mapping = new RenderComponentMapping(
                        RenderSwizzle.Zero, RenderSwizzle.Zero, RenderSwizzle.Zero, RenderSwizzle.R);

                    return true;
                }

                if ((pixelFlags & PixelFormatLuminance) != 0)
                {
                    mapping = new RenderComponentMapping(
                        RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.One);

                    return true;
                }

                Logger.Warning?.PrintMsg(LogClass.Application,
                    $"{name}: 8bpp with neither the alpha nor luminance flag (0x{pixelFlags:X}); " +
                    "treating it as luminance");

                mapping = new RenderComponentMapping(
                    RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.R, RenderSwizzle.One);

                return true;
            }

            Logger.Error?.PrintMsg(LogClass.Application,
                $"{name}: unhandled pixel format, flags 0x{pixelFlags:X} at {bitCount} bpp");

            format = RenderFormat.Unknown;
            return false;
        }

        private static string FourCcText(uint fourCc) => new(
        [
            (char)(fourCc & 0xFF),
            (char)((fourCc >> 8) & 0xFF),
            (char)((fourCc >> 16) & 0xFF),
            (char)((fourCc >> 24) & 0xFF),
        ]);
    }
}
