using System.Numerics;
using System.Runtime.InteropServices;
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

    public class GridShader : Shader<GridUniforms>
    {
        public GridShader(
            SlDevice device,
            string shaderSource)
            : base(
                device,
                shaderSource,
                "Grid Shader",
                pipelineVariants: new Dictionary<string, SlPipelineVariantDescriptor>
                {
                    ["default"] = new()
                    {
                        Topology = SlPrimitiveTopology.LineList,
                        CullMode = SlCullMode.None,
                        FrontFace = SlFrontFace.Clockwise,
                        DepthWrite = true,
                        DepthCompare = SlCompareFunction.Greater,
                        AlphaBlend = true
                    }
                })
        {
        }

        protected override SlVertexBufferLayout[] CreateVertexLayouts()
        {
            var vertexAttributes = new SlVertexAttribute[2];
            vertexAttributes[0] = new SlVertexAttribute { Format = SlVertexFormat.Float32x3, Offset = 0,  ShaderLocation = 0 };  // Position
            vertexAttributes[1] = new SlVertexAttribute { Format = SlVertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };  // Color

            return
            [
                new SlVertexBufferLayout
                {
                    Stride = 24,
                    StepMode = SlVertexStepMode.Vertex,
                    Attributes = vertexAttributes
                }
            ];
        }
    }
}
