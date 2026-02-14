using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Solaris.RHI;
using Solaris.Wgpu;

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

    public unsafe class SkyboxShader : WgpuShader<SkyboxUniforms>
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

        protected override VertexBufferLayout[] CreateVertexLayouts()
        {
            var vertexAttrib = new VertexAttribute[1];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 };

            Attributes = GCHandle.Alloc(vertexAttrib, GCHandleType.Pinned);

            return
            [
                new VertexBufferLayout
                {
                    ArrayStride = 12,
                    StepMode = VertexStepMode.Vertex,
                    AttributeCount = (uint)vertexAttrib.Length,
                    Attributes = (VertexAttribute*)Attributes.AddrOfPinnedObject()
                }
            ];
        }
    }
}
