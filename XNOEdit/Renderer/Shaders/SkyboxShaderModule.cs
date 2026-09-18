using System.Numerics;
using System.Runtime.InteropServices;
using Plume;
using Solaris;

namespace XNOEdit.Renderer.Shaders
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SkyboxPushConstants
    {
        public Matrix4x4 InverseViewProjection;
        public Vector4 CameraPosition;
        public Vector4 SunDirection;
        public Vector4 SunColor;
    }

    public class SkyboxShader : ShaderModule
    {
        public const uint VertexStride = 12;

        public SkyboxShader(SlDevice device, ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> pixel)
            : base(device, vertex, pixel, "Skybox Shader",
                new Dictionary<string, SlPipelineVariant>
                {
                    ["default"] = new()
                    {
                        Topology = RenderPrimitiveTopology.TriangleStrip,
                        CullMode = RenderCullMode.None,
                        FrontFace = RenderFrontFace.Clockwise,
                        DepthWrite = false,
                        DepthCompare = RenderComparisonFunction.Always,
                        DepthTest = false
                    }
                })
        {
        }

        protected override SlVertexLayout CreateVertexLayout() => new(
            new SlVertexBufferLayout(0, VertexStride, SlVertexStepMode.Vertex,
            [
                new SlVertexAttribute("POSITION", 0, 0, RenderFormat.R32G32B32Float, 0),
            ]));
    }
}
