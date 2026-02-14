using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Solaris.RHI;

namespace XNOEdit.Renderer.Shaders
{
    public unsafe class InstancedModelShader : ModelShader
    {
        private GCHandle _instanceAttributes;

        public InstancedModelShader(
            SlDevice device,
            string shaderSource)
            : base(device, shaderSource)
        {
        }

        protected override VertexBufferLayout[] CreateVertexLayouts()
        {
            // Vertex attributes (same as base)
            var vertexAttrib = new VertexAttribute[5];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0,  ShaderLocation = 0 };
            vertexAttrib[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };
            vertexAttrib[2] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 24, ShaderLocation = 2 };
            vertexAttrib[3] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 40, ShaderLocation = 3 };
            vertexAttrib[4] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 48, ShaderLocation = 4 };

            Attributes = GCHandle.Alloc(vertexAttrib, GCHandleType.Pinned);

            // Instance attributes (mat4x4 as 4 vec4s)
            var instanceAttrib = new VertexAttribute[4];
            instanceAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 0,  ShaderLocation = 5 };
            instanceAttrib[1] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 16, ShaderLocation = 6 };
            instanceAttrib[2] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 32, ShaderLocation = 7 };
            instanceAttrib[3] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 48, ShaderLocation = 8 };

            _instanceAttributes = GCHandle.Alloc(instanceAttrib, GCHandleType.Pinned);

            return
            [
                new VertexBufferLayout
                {
                    ArrayStride = 56,
                    StepMode = VertexStepMode.Vertex,
                    AttributeCount = (uint)vertexAttrib.Length,
                    Attributes = (VertexAttribute*)Attributes.AddrOfPinnedObject()
                },
                new VertexBufferLayout
                {
                    ArrayStride = 64, // sizeof(Matrix4x4)
                    StepMode = VertexStepMode.Instance,
                    AttributeCount = (uint)instanceAttrib.Length,
                    Attributes = (VertexAttribute*)_instanceAttributes.AddrOfPinnedObject()
                }
            ];
        }

        public override void Dispose()
        {
            if (_instanceAttributes.IsAllocated)
                _instanceAttributes.Free();

            base.Dispose();
        }
    }
}
