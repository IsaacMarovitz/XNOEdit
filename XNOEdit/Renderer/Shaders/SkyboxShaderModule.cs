using System.Numerics;
using System.Runtime.InteropServices;
using Solaris.RHI;

namespace XNOEdit.Renderer.Shaders
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SkyboxUniforms
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 SunDirection;
        public Vector4 SunColor;
    }

    public unsafe class SkyboxShader : Shader<SkyboxUniforms>
    {
        public SkyboxShader(
            SlDevice device,
            string shaderSource)
            : base(
                device,
                shaderSource,
                "Skybox Shader",
                pipelineVariants: new Dictionary<string, SlPipelineVariantDescriptor>
                {
                    ["default"] = new()
                    {
                        Topology = SlPrimitiveTopology.TriangleStrip,
                        CullMode = SlCullMode.None,
                        FrontFace = SlFrontFace.Clockwise,
                        DepthWrite = false,
                        DepthCompare = SlCompareFunction.Always
                    }
                })
        {
        }

        protected override SlVertexBufferLayout[] CreateVertexLayouts()
        {
            var vertexAttribute = new SlVertexAttribute { Format = SlVertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 };

            return
            [
                new SlVertexBufferLayout
                {
                    Stride = 12,
                    StepMode = SlVertexStepMode.Vertex,
                    Attributes = [vertexAttribute]
                }
            ];
        }
    }
}
