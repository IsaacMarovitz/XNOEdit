namespace Solaris.Metal
{
    /// <summary>
    /// Maps RHI bind groups to Metal's flat index spaces.
    ///
    /// Metal limits: buffer 0–30, sampler 0–15, texture 0–127.
    ///
    /// Buffer indices:
    ///   0..3  — vertex buffers (set via encoder.SetVertexBuffer, NOT argument table)
    ///   4..7  — bind group 0 buffers (argument table, device pointers in shader)
    ///   8..11 — bind group 1 buffers
    ///   12..15 — bind group 2 buffers
    ///   16..19 — bind group 3 buffers
    ///
    /// Sampler indices (0–15):
    ///   0..3  — bind group 0
    ///   4..7  — bind group 1
    ///   8..11 — bind group 2
    ///   12..15 — bind group 3
    ///
    /// Texture indices (0–127):
    ///   0..7  — bind group 0
    ///   8..15 — bind group 1
    ///   16..23 — bind group 2
    ///   24..31 — bind group 3
    /// </summary>
    internal static class MetalBindingLayout
    {
        public const uint MaxVertexBufferSlots = 4;
        public const uint MaxBindGroups = 4;
        public const uint BuffersPerGroup = 4;
        public const uint SamplersPerGroup = 4;
        public const uint TexturesPerGroup = 8;
        public const uint BindGroupBufferBase = MaxVertexBufferSlots;

        public const uint MaxBufferBindings = BindGroupBufferBase + MaxBindGroups * BuffersPerGroup;
        public const uint MaxSamplerBindings = MaxBindGroups * SamplersPerGroup;
        public const uint MaxTextureBindings = MaxBindGroups * TexturesPerGroup;

        public static uint BufferIndex(uint group, uint binding)
            => BindGroupBufferBase + group * BuffersPerGroup + binding;

        public static uint SamplerIndex(uint group, uint binding)
            => group * SamplersPerGroup + binding;

        public static uint TextureIndex(uint group, uint binding)
            => group * TexturesPerGroup + binding;
    }
}
