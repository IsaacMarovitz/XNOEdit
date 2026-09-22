using System.Numerics;
using Solaris;
using Solaris.Graph;
using XNOEdit.Render.Shaders;

namespace XNOEdit.Render.Renderers
{
    public struct SkyboxParameters
    {
        public Vector3 CameraPosition;
        public Vector3 SunDirection;
        public Vector3 SunColor;
    }

    public class SkyboxRenderer : Renderer<SkyboxParameters>
    {
        private readonly SlDevice _device;
        private readonly SlBuffer _vertexBuffer;

        public SkyboxRenderer(SlDevice device)
            : base(CreateShader(device))
        {
            _device = device;

            float[] vertices =
            [
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f,  1.0f, 0.0f
            ];

            _vertexBuffer = device.CreateBuffer(vertices, SlBufferUsage.Vertex, "SkyboxVertices");
        }

        public static SkyboxShader CreateShader(SlDevice device)
        {
            return new SkyboxShader(device, ShaderLibrary.Get(device, "skybox_vs"),
                ShaderLibrary.Get(device, "skybox_ps"));
        }

        public override void Draw(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            SkyboxParameters skyboxParameters)
        {
            var rotationOnlyView = view;
            rotationOnlyView.M41 = 0.0f;
            rotationOnlyView.M42 = 0.0f;
            rotationOnlyView.M43 = 0.0f;

            Matrix4x4.Invert(rotationOnlyView * projection, out var inverse);

            var constants = new SkyboxPushConstants
            {
                InverseViewProjection = inverse,
                CameraPosition = skyboxParameters.CameraPosition.AsVector4(),
                SunDirection = skyboxParameters.SunDirection.AsVector4(),
                SunColor = skyboxParameters.SunColor.AsVector4()
            };

            ctx.SetPipeline(ShaderModule.Pipeline(ctx.Signature));
            ctx.PushConstants(in constants);
            ctx.SetVertexBuffer(0, _vertexBuffer.View, SkyboxShader.VertexStride);
            ctx.Draw(4);
        }

        public override void Dispose()
        {
            _device.Retire(_vertexBuffer);

            base.Dispose();
        }
    }
}
