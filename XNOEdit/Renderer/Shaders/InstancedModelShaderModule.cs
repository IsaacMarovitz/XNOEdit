using Plume;
using Solaris;

namespace XNOEdit.Renderer.Shaders
{
    /// <summary>
    /// The instanced variant of <see cref="ModelShader"/>. Shares its pipeline variants
    /// and per-mesh constant layout; the only difference is a second vertex buffer at
    /// slot 1 carrying a per-instance transform.
    /// </summary>
    public class InstancedModelShader : ModelShader
    {
        /// <summary>Stride of the per-instance buffer: one 4x4 transform.</summary>
        public const uint InstanceStride = 64;

        public InstancedModelShader(SlDevice device, ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> pixel)
            : base(device, vertex, pixel, "Instanced Model Shader")
        {
        }

        protected override SlVertexLayout CreateVertexLayout() => new(
            new SlVertexBufferLayout(0, VertexStride, SlVertexStepMode.Vertex,
            [
                new SlVertexAttribute("POSITION", 0, 0, RenderFormat.R32G32B32Float, 0),
                new SlVertexAttribute("NORMAL", 0, 1, RenderFormat.R32G32B32Float, 12),
                new SlVertexAttribute("COLOR", 0, 2, RenderFormat.R32G32B32A32Float, 24),
                new SlVertexAttribute("TEXCOORD", 0, 3, RenderFormat.R32G32Float, 40),
                new SlVertexAttribute("TEXCOORD", 1, 4, RenderFormat.R32G32Float, 48),
            ]),
            new SlVertexBufferLayout(1, InstanceStride, SlVertexStepMode.Instance,
            [
                // A mat4x4 crosses four attribute slots; one row each.
                new SlVertexAttribute("INSTANCE", 0, 5, RenderFormat.R32G32B32A32Float, 0),
                new SlVertexAttribute("INSTANCE", 1, 6, RenderFormat.R32G32B32A32Float, 16),
                new SlVertexAttribute("INSTANCE", 2, 7, RenderFormat.R32G32B32A32Float, 32),
                new SlVertexAttribute("INSTANCE", 3, 8, RenderFormat.R32G32B32A32Float, 48),
            ]));
    }
}
