using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Solaris.RHI;
using Solaris.Wgpu;

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

    public unsafe class GridShader : WgpuShader<GridUniforms>
    {
        public GridShader(
            SlDevice device,
            string shaderSource)
            : base(
                device,
                shaderSource,
                "Grid Shader",
                pipelineVariants: new Dictionary<string, PipelineVariantDescriptor>
                {
                    ["default"] = new()
                    {
                        Topology = PrimitiveTopology.LineList,
                        CullMode = CullMode.None,
                        FrontFace = FrontFace.CW,
                        DepthWrite = true,
                        DepthCompare = CompareFunction.Greater,
                        AlphaBlend = true
                    }
                })
        {
        }

        protected override VertexBufferLayout[] CreateVertexLayouts()
        {
            var vertexAttrib = new VertexAttribute[2];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0,  ShaderLocation = 0 };  // Position
            vertexAttrib[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };  // Color

            Attributes = GCHandle.Alloc(vertexAttrib, GCHandleType.Pinned);

            return
            [
                new VertexBufferLayout
                {
                    ArrayStride = 24,
                    StepMode = VertexStepMode.Vertex,
                    AttributeCount = (uint)vertexAttrib.Length,
                    Attributes = (VertexAttribute*)Attributes.AddrOfPinnedObject()
                }
            ];
        }
    }
}
