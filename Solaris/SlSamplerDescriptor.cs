using Plume;

namespace Solaris
{
    public readonly record struct SlSamplerDescriptor
    {
        public RenderFilter MinFilter { get; init; } = RenderFilter.Linear;
        public RenderFilter MagFilter { get; init; } = RenderFilter.Linear;
        public RenderMipmapMode MipmapMode { get; init; } = RenderMipmapMode.Linear;
        public RenderTextureAddressMode AddressU { get; init; } = RenderTextureAddressMode.Wrap;
        public RenderTextureAddressMode AddressV { get; init; } = RenderTextureAddressMode.Wrap;
        public RenderTextureAddressMode AddressW { get; init; } = RenderTextureAddressMode.Wrap;
        public float MipLodBias { get; init; }
        public uint MaxAnisotropy { get; init; } = 16;
        public bool AnisotropyEnabled { get; init; }
        public RenderComparisonFunction ComparisonFunction { get; init; } = RenderComparisonFunction.Never;
        public bool ComparisonEnabled { get; init; }
        public RenderBorderColor BorderColor { get; init; } = RenderBorderColor.OpaqueBlack;
        public float MinLod { get; init; }
        public float MaxLod { get; init; } = float.MaxValue;

        public SlSamplerDescriptor() { }

        public static SlSamplerDescriptor LinearWrap { get; } = new()
        {
            MinFilter = RenderFilter.Linear,
            MagFilter = RenderFilter.Linear,
            MipmapMode = RenderMipmapMode.Linear,
            AddressU = RenderTextureAddressMode.Wrap,
            AddressV = RenderTextureAddressMode.Wrap,
            AddressW = RenderTextureAddressMode.Wrap,
            MaxAnisotropy = 16,
            ComparisonFunction = RenderComparisonFunction.Never,
            BorderColor = RenderBorderColor.OpaqueBlack,
            MinLod = 0.0f,
            MaxLod = float.MaxValue,
        };

        public static SlSamplerDescriptor LinearClamp { get; } = LinearWrap with
        {
            AddressU = RenderTextureAddressMode.Clamp,
            AddressV = RenderTextureAddressMode.Clamp,
            AddressW = RenderTextureAddressMode.Clamp,
        };

        public static SlSamplerDescriptor NearestClamp { get; } = LinearClamp with
        {
            MinFilter = RenderFilter.Nearest,
            MagFilter = RenderFilter.Nearest,
            MipmapMode = RenderMipmapMode.Nearest,
        };

        internal RenderSamplerDesc ToPlume() => new()
        {
            MinFilter = MinFilter,
            MagFilter = MagFilter,
            MipmapMode = MipmapMode,
            AddressU = AddressU,
            AddressV = AddressV,
            AddressW = AddressW,
            MipLodBias = MipLodBias,
            MaxAnisotropy = MaxAnisotropy,
            AnisotropyEnabled = AnisotropyEnabled,
            ComparisonFunc = ComparisonFunction,
            ComparisonEnabled = ComparisonEnabled,
            BorderColor = BorderColor,
            MinLod = MinLod,
            MaxLod = MaxLod,
            ShaderVisibility = RenderShaderVisibility.All,
        };
    }
}
