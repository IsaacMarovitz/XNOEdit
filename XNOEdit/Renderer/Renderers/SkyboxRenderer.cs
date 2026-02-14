using System.Numerics;
using Solaris;
using XNOEdit.Renderer.Shaders;

namespace XNOEdit.Renderer.Renderers
{
    public struct SkyboxParameters
    {
        public Vector3 SunDirection;
        public Vector3 SunColor;
    }

    public class SkyboxRenderer : Renderer<SkyboxParameters>
    {
        private readonly SlBuffer<float> _vertexBuffer;

        public SkyboxRenderer(SlDevice device)
            : base(CreateShader(device))
        {
            var vertices = new[]
            {
                -1.0f, -1.0f, 0.0f,
                1.0f,  -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f,   1.0f, 0.0f
            };

            _vertexBuffer = device.CreateBuffer(vertices, SlBufferUsage.Vertex);
        }

        private static SkyboxShader CreateShader(SlDevice device)
        {
            return new SkyboxShader(device, EmbeddedResources.ReadAllText("XNOEdit/Shaders/Skybox.wgsl"));
        }

        public override void Draw(
            SlQueue queue,
            SlRenderPass passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            SkyboxParameters skyboxParameters)
        {
            base.Draw(queue, passEncoder, view, projection, skyboxParameters);

            var uniforms = new SkyboxUniforms
            {
                View = view,
                Projection = projection,
                SunDirection = skyboxParameters.SunDirection.AsVector4(),
                SunColor = skyboxParameters.SunColor.AsVector4()
            };

            ((SkyboxShader)Shader).UpdateUniforms(queue, in uniforms);

            passEncoder.SetPipeline(Shader.GetPipeline());
            passEncoder.SetVertexBuffer(0, _vertexBuffer);
            passEncoder.Draw(4);
        }

        public override void Dispose()
        {
            _vertexBuffer.Dispose();

            base.Dispose();
        }
    }
}
