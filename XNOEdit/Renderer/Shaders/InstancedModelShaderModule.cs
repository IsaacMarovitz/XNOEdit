using Solaris.RHI;

namespace XNOEdit.Renderer.Shaders
{
    public class InstancedModelShader : ModelShader
    {
        public InstancedModelShader(
            SlDevice device,
            string shaderSource)
            : base(device, shaderSource)
        {
        }

        protected override SlVertexBufferLayout[] CreateVertexLayouts()
        {
            // Vertex attributes (same as base)
            var vertexAttributes = new SlVertexAttribute[5];
            vertexAttributes[0] = new SlVertexAttribute { Format = SlVertexFormat.Float32x3, Offset = 0,  ShaderLocation = 0 };
            vertexAttributes[1] = new SlVertexAttribute { Format = SlVertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };
            vertexAttributes[2] = new SlVertexAttribute { Format = SlVertexFormat.Float32x4, Offset = 24, ShaderLocation = 2 };
            vertexAttributes[3] = new SlVertexAttribute { Format = SlVertexFormat.Float32x2, Offset = 40, ShaderLocation = 3 };
            vertexAttributes[4] = new SlVertexAttribute { Format = SlVertexFormat.Float32x2, Offset = 48, ShaderLocation = 4 };

            // Instance attributes (mat4x4 as 4 vec4s)
            var instanceAttributes = new SlVertexAttribute[4];
            instanceAttributes[0] = new SlVertexAttribute { Format = SlVertexFormat.Float32x4, Offset = 0,  ShaderLocation = 5 };
            instanceAttributes[1] = new SlVertexAttribute { Format = SlVertexFormat.Float32x4, Offset = 16, ShaderLocation = 6 };
            instanceAttributes[2] = new SlVertexAttribute { Format = SlVertexFormat.Float32x4, Offset = 32, ShaderLocation = 7 };
            instanceAttributes[3] = new SlVertexAttribute { Format = SlVertexFormat.Float32x4, Offset = 48, ShaderLocation = 8 };

            return
            [
                new SlVertexBufferLayout
                {
                    Stride = 56,
                    StepMode = SlVertexStepMode.Vertex,
                    Attributes = vertexAttributes
                },
                new SlVertexBufferLayout
                {
                    Stride = 64, // sizeof(Matrix4x4)
                    StepMode = SlVertexStepMode.Instance,
                    Attributes = instanceAttributes
                }
            ];
        }
    }
}
