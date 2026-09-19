using System.Numerics;
using System.Runtime.InteropServices;
using Plume;
using Solaris;

namespace XNOEdit.Renderer.Shaders
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GridUniforms
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector3 CameraPos;
        public float FadeStart;
        public float FadeEnd;
    }

    public class GridShader : ShaderModule
    {
        public GridShader(SlDevice device, ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> pixel)
            : base(device, vertex, pixel, "Grid Shader",
                new Dictionary<string, SlPipelineVariant>
                {
                    ["default"] = new()
                    {
                        Topology = RenderPrimitiveTopology.LineList,
                        CullMode = RenderCullMode.None,
                        FrontFace = RenderFrontFace.Clockwise,
                        DepthWrite = true,
                        DepthCompare = RenderComparisonFunction.Greater,
                        AlphaBlend = true
                    }
                })
        {
        }

        protected override SlVertexLayout CreateVertexLayout() => new(
            new SlVertexBufferLayout(0, 24, SlVertexStepMode.Vertex,
            [
                new SlVertexAttribute("POSITION", 0, 0, RenderFormat.R32G32B32Float, 0),
                new SlVertexAttribute("COLOR", 0, 1, RenderFormat.R32G32B32Float, 12),
            ]));
    }
}
