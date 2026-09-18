namespace Solaris
{
    /// <summary>
    /// An index into the global bindless texture table. Stable for the lifetime of the
    /// texture it was issued for, including across table growth.
    /// </summary>
    public readonly struct SlTextureIndex : IEquatable<SlTextureIndex>
    {
        public const uint IndexMask = 0x00FFFFFF;
        public const uint FlagMask = 0xFF000000;

        /// <summary>Slot 0 is a 1x1 all-zero 2D texture, so <c>default</c> samples black.</summary>
        public static readonly SlTextureIndex NullTexture2D = new(0);
        public static readonly SlTextureIndex NullTexture2DArray = new(1);
        public static readonly SlTextureIndex NullTextureCube = new(2);

        /// <summary>A 1x1 texture that samples opaque white, for "no texture" material slots.</summary>
        public static readonly SlTextureIndex WhiteTexture2D = new(3);

        internal const uint ReservedCount = 4;

        private readonly uint _value;

        internal SlTextureIndex(uint value) => _value = value;

        public uint Slot => _value & IndexMask;

        public uint Flags => _value & FlagMask;

        /// <summary>Raw value to push to the shader, flags included.</summary>
        public uint Packed => _value;

        public SlTextureIndex WithFlags(uint flags) => new((_value & IndexMask) | (flags & FlagMask));

        public bool Equals(SlTextureIndex other) => _value == other._value;

        public override bool Equals(object? obj) => obj is SlTextureIndex other && Equals(other);

        public override int GetHashCode() => (int)_value;

        public override string ToString() => Flags == 0 ? $"tex:{Slot}" : $"tex:{Slot}|{Flags:X8}";

        public static bool operator ==(SlTextureIndex left, SlTextureIndex right) => left.Equals(right);

        public static bool operator !=(SlTextureIndex left, SlTextureIndex right) => !left.Equals(right);
    }

    /// <summary>
    /// An index into the global bindless sampler table. Samplers are interned by
    /// descriptor, so two identical descriptors always yield the same index.
    /// </summary>
    public readonly struct SlSamplerIndex : IEquatable<SlSamplerIndex>
    {
        /// <summary>Slot 0 is a linear-wrap sampler, so <c>default</c> is always usable.</summary>
        public static readonly SlSamplerIndex Default = new(0);

        internal const uint ReservedCount = 1;

        private readonly uint _value;

        internal SlSamplerIndex(uint value) => _value = value;

        public uint Slot => _value;

        public bool Equals(SlSamplerIndex other) => _value == other._value;

        public override bool Equals(object? obj) => obj is SlSamplerIndex other && Equals(other);

        public override int GetHashCode() => (int)_value;

        public override string ToString() => $"smp:{_value}";

        public static bool operator ==(SlSamplerIndex left, SlSamplerIndex right) => left.Equals(right);

        public static bool operator !=(SlSamplerIndex left, SlSamplerIndex right) => !left.Equals(right);
    }
}
