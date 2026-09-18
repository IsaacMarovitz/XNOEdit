using Plume;

namespace Solaris
{
    public readonly record struct SlPipelineVariant
    {
        public RenderPrimitiveTopology Topology { get; init; } = RenderPrimitiveTopology.TriangleList;
        public RenderCullMode CullMode { get; init; } = RenderCullMode.None;
        public RenderFrontFace FrontFace { get; init; } = RenderFrontFace.Clockwise;
        public bool DepthWrite { get; init; } = true;
        public RenderComparisonFunction DepthCompare { get; init; } = RenderComparisonFunction.Greater;
        public bool AlphaBlend { get; init; }

        /// <summary>
        /// Explicit blend state, overriding <see cref="AlphaBlend"/> when set. Use this
        /// for additive, multiply or premultiplied targets rather than adding more bools.
        /// </summary>
        public SlBlendState? Blend { get; init; }

        /// <summary>
        /// Depth bias, for coplanar geometry like decals. Requires
        /// <see cref="SlDeviceCapabilities.DynamicDepthBias"/> only if changed per draw;
        /// baked into the pipeline otherwise.
        /// </summary>
        public SlDepthBias? DepthBias { get; init; }

        /// <summary>Set to disable depth testing entirely rather than just writes.</summary>
        public bool? DepthTest { get; init; }

        /// <summary>Per-channel colour write mask. Defaults to all channels.</summary>
        public RenderColorWriteEnable? ColorWriteMask { get; init; }

        public SlPipelineVariant() { }

        internal RenderBlendDesc ToBlendDesc()
        {
            var mask = (byte)(ColorWriteMask ?? RenderColorWriteEnable.All);

            if (Blend is { } explicitBlend)
                return explicitBlend.ToPlume(mask);

            if (!AlphaBlend)
            {
                return new RenderBlendDesc
                {
                    BlendEnabled = false,
                    SrcBlend = RenderBlend.One,
                    DstBlend = RenderBlend.Zero,
                    BlendOp = RenderBlendOperation.Add,
                    SrcBlendAlpha = RenderBlend.One,
                    DstBlendAlpha = RenderBlend.Zero,
                    BlendOpAlpha = RenderBlendOperation.Add,
                    RenderTargetWriteMask = mask,
                };
            }

            return SlBlendState.AlphaBlend.ToPlume(mask);
        }
    }

    public readonly record struct SlBlendState(
        RenderBlend SrcColor,
        RenderBlend DstColor,
        RenderBlendOperation ColorOp,
        RenderBlend SrcAlpha,
        RenderBlend DstAlpha,
        RenderBlendOperation AlphaOp)
    {
        public static SlBlendState AlphaBlend { get; } = new(
            RenderBlend.SrcAlpha, RenderBlend.InvSrcAlpha, RenderBlendOperation.Add,
            RenderBlend.One, RenderBlend.InvSrcAlpha, RenderBlendOperation.Add);

        public static SlBlendState Additive { get; } = new(
            RenderBlend.SrcAlpha, RenderBlend.One, RenderBlendOperation.Add,
            RenderBlend.One, RenderBlend.One, RenderBlendOperation.Add);

        public static SlBlendState Premultiplied { get; } = new(
            RenderBlend.One, RenderBlend.InvSrcAlpha, RenderBlendOperation.Add,
            RenderBlend.One, RenderBlend.InvSrcAlpha, RenderBlendOperation.Add);

        internal RenderBlendDesc ToPlume(byte writeMask) => new()
        {
            BlendEnabled = true,
            SrcBlend = SrcColor,
            DstBlend = DstColor,
            BlendOp = ColorOp,
            SrcBlendAlpha = SrcAlpha,
            DstBlendAlpha = DstAlpha,
            BlendOpAlpha = AlphaOp,
            RenderTargetWriteMask = writeMask,
        };
    }

    public readonly record struct SlDepthBias(float Constant, float Clamp, float SlopeScaled);
}
