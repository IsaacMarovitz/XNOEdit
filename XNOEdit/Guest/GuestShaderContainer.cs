using System.Buffers.Binary;
using System.IO.Hashing;

namespace XNOEdit.Guest
{
    public enum GuestShaderStage
    {
        Pixel,
        Vertex,
    }

    public sealed class GuestShaderContainer
    {
        /// <summary>Nine big-endian uint32 fields.</summary>
        public const int HeaderSize = 36;

        private const uint FlagMask = 0xFFFFFF00;
        private const uint FlagMatch = 0x102A1100;

        private GuestShaderContainer(int offset, ulong hash, GuestShaderStage stage)
        {
            Offset = offset;
            Hash = hash;
            Stage = stage;
        }

        public int Offset { get; }

        public ulong Hash { get; }

        public GuestShaderStage Stage { get; }

        public static List<GuestShaderContainer> Scan(byte[] file)
        {
            ArgumentNullException.ThrowIfNull(file);

            var results = new List<GuestShaderContainer>(2);

            if (file.Length <= HeaderSize)
                return results;

            var i = 0;

            while (i < file.Length - HeaderSize - 1)
            {
                var span = file.AsSpan(i);

                var flags = BinaryPrimitives.ReadUInt32BigEndian(span);
                var virtualSize = BinaryPrimitives.ReadUInt32BigEndian(span[4..]);
                var physicalSize = BinaryPrimitives.ReadUInt32BigEndian(span[8..]);
                var field1C = BinaryPrimitives.ReadUInt32BigEndian(span[28..]);
                var field20 = BinaryPrimitives.ReadUInt32BigEndian(span[32..]);

                var dataSize = (long)virtualSize + physicalSize;

                if ((flags & FlagMask) == FlagMatch &&
                    dataSize <= file.Length - i &&
                    dataSize >= HeaderSize &&
                    field1C == 0 &&
                    field20 == 0)
                {
                    var size = (int)dataSize;
                    var container = file.AsSpan(i, size);

                    // Bit 0 set means vertex; XenosRecomp derives isPixelShader from its absence.
                    var stage = (flags & 0x1) != 0 ? GuestShaderStage.Vertex : GuestShaderStage.Pixel;

                    results.Add(new GuestShaderContainer(
                        i,
                        XxHash3.HashToUInt64(container),
                        stage));

                    i += size;
                }
                else
                {
                    i += sizeof(uint);
                }
            }

            return results;
        }
    }
}
