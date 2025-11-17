using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer.Shaders
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BasicModelUniforms
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 LightDir;
        public Vector4 LightColor;
        public Vector3 ViewPos;
        public float VertColorStrength;
    }

    public unsafe class ModelShader : WgpuShader<BasicModelUniforms>
    {
        public ModelShader(
            WebGPU wgpu,
            Device* device,
            string shaderSource,
            string label,
            TextureFormat colorFormat)
            : base(
                wgpu,
                device,
                shaderSource,
                label,
                colorFormat,
                pipelineVariants: new Dictionary<string, PipelineVariantDescriptor>
                {
                    ["default"] = new()
                    {
                        Topology = PrimitiveTopology.TriangleList,
                        CullMode = CullMode.None,
                        FrontFace = FrontFace.CW,
                        DepthWrite = true,
                        DepthCompare = CompareFunction.Less,
                        AlphaBlend = true
                    },
                    ["culled"] = new()
                    {
                        Topology = PrimitiveTopology.TriangleList,
                        CullMode = CullMode.Back,
                        FrontFace = FrontFace.CW,
                        DepthWrite = true,
                        DepthCompare = CompareFunction.Less,
                        AlphaBlend = true
                    },
                    ["wireframe"] = new()
                    {
                        Topology = PrimitiveTopology.LineList,
                        CullMode = CullMode.None,
                        FrontFace = FrontFace.CW,
                        DepthWrite = true,
                        DepthCompare = CompareFunction.Less,
                        AlphaBlend = false
                    }
                })
        {
        }

        protected override VertexBufferLayout[] CreateVertexLayouts()
        {
            var vertexAttrib = new VertexAttribute[3];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0,  ShaderLocation = 0 };  // Position
            vertexAttrib[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };  // Normal
            vertexAttrib[2] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 24, ShaderLocation = 2 };  // Color

            Attributes = GCHandle.Alloc(vertexAttrib, GCHandleType.Pinned);

            return
            [
                new VertexBufferLayout
                {
                    ArrayStride = 40,
                    StepMode = VertexStepMode.Vertex,
                    AttributeCount = (uint)vertexAttrib.Length,
                    Attributes = (VertexAttribute*)Attributes.AddrOfPinnedObject()
                }
            ];
        }

        public RenderPipeline* GetPipeline(bool cullBackfaces, bool wireframe)
        {
            if (wireframe)
                return GetPipeline("wireframe");

            return cullBackfaces ? GetPipeline("culled") : GetPipeline("default");
        }
    }
}
