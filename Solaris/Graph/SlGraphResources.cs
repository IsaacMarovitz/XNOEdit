using Plume;

namespace Solaris.Graph
{
    /// <summary>
    /// A virtual reference to a texture within one frame.
    /// </summary>
    public readonly struct SlTextureHandle(int id, int version) : IEquatable<SlTextureHandle>
    {
        internal readonly int Id = id;
        internal readonly int Version = version;

        public bool IsValid => Id > 0;

        internal SlTextureHandle NextVersion() => new(Id, Version + 1);

        public bool Equals(SlTextureHandle other) => Id == other.Id && Version == other.Version;

        public override bool Equals(object? obj) => obj is SlTextureHandle other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Id, Version);

        public override string ToString() => $"tex#{Id}.v{Version}";
    }

    public enum SlLoadOp
    {
        Discard,
        Clear,
        Load,
    }

    internal enum SlAccess
    {
        ColorTarget,
        DepthTarget,
        ShaderRead,
        CopySource,
        CopyDest,
    }

    internal static class SlAccessExtensions
    {
        public static RenderTextureLayout ToLayout(this SlAccess access) => access switch
        {
            SlAccess.ColorTarget => RenderTextureLayout.ColorWrite,
            SlAccess.DepthTarget => RenderTextureLayout.DepthWrite,
            SlAccess.ShaderRead => RenderTextureLayout.ShaderRead,
            SlAccess.CopySource => RenderTextureLayout.CopySource,
            SlAccess.CopyDest => RenderTextureLayout.CopyDest,
            _ => RenderTextureLayout.General,
        };

        public static bool IsWrite(this SlAccess access) =>
            access is SlAccess.ColorTarget or SlAccess.DepthTarget or SlAccess.CopyDest;
    }

    internal sealed class SlResourceEntry
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public SlTexture? Texture { get; set; }

        /// <summary>True when the graph allocated it and must retire it after the frame.</summary>
        public bool IsTransient { get; set; }

        /// <summary>True for the swap chain back buffer, which must end the frame in Present.</summary>
        public bool IsSwapChainTarget { get; set; }

        /// <summary>
        /// Layout as the GPU will see it at this point in the recorded stream. Persisted
        /// across frames for imported resources.
        /// </summary>
        public RenderTextureLayout Layout { get; set; } = RenderTextureLayout.Unknown;

        public int Version { get; set; }
    }

    internal readonly struct SlPassAccess(SlTextureHandle handle, SlAccess access, SlLoadOp load, SlClearValue clear)
    {
        public readonly SlTextureHandle Handle = handle;
        public readonly SlAccess Access = access;
        public readonly SlLoadOp Load = load;
        public readonly SlClearValue Clear = clear;
    }

    public readonly struct SlClearValue
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;
        public readonly float Depth;

        private SlClearValue(float r, float g, float b, float a, float depth)
        {
            R = r;
            G = g;
            B = b;
            A = a;
            Depth = depth;
        }

        public static SlClearValue Color(float r, float g, float b, float a = 1.0f) => new(r, g, b, a, 0.0f);

        public static SlClearValue DepthValue(float depth) => new(0, 0, 0, 0, depth);

        internal RenderColor ToRenderColor() => new(R, G, B, A);
    }

    internal sealed class SlPassNode
    {
        public required int Index { get; init; }
        public required string Name { get; init; }
        public List<SlPassAccess> Accesses { get; } = [];
        public Action<SlPassContext>? Body { get; set; }
        public List<int> Dependencies { get; } = [];
    }
}
