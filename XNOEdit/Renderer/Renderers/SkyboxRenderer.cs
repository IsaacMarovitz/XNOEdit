using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Shaders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer.Renderers
{
    public struct SkyboxParameters
    {
        public Vector3 SunDirection;
        public Vector3 SunColor;
    }

    public unsafe class SkyboxRenderer : WgpuRenderer<SkyboxParameters>
    {
        private readonly WgpuBuffer<float> _vertexBuffer;

        public SkyboxRenderer(WebGPU wgpu, WgpuDevice device)
            : base(wgpu, CreateShader(wgpu, device))
        {
            var vertices = new[]
            {
                -1.0f, -1.0f, 0.0f,
                1.0f,  -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f,   1.0f, 0.0f
            };

            _vertexBuffer = new WgpuBuffer<float>(wgpu, device, vertices, BufferUsage.Vertex);
        }

        private static SkyboxShader CreateShader(WebGPU wgpu, WgpuDevice device)
        {
            return new SkyboxShader(wgpu, device, EmbeddedResources.ReadAllText("XNOEdit/Shaders/Skybox.wgsl"));
        }

        public override void Draw(
            Queue* queue,
            RenderPassEncoder* passEncoder,
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
            Wgpu.RenderPassEncoderSetPipeline(passEncoder, Shader.GetPipeline());
            Wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            Wgpu.RenderPassEncoderDraw(passEncoder, 4, 1, 0, 0);
        }

        public override void Dispose()
        {
            _vertexBuffer?.Dispose();

            base.Dispose();
        }
    }
}
