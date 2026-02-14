using System.Numerics;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Solaris.RHI;
using Solaris.Wgpu;
using XNOEdit.Renderer.Shaders;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer.Renderers
{
    public struct SkyboxParameters
    {
        public Vector3 SunDirection;
        public Vector3 SunColor;
    }

    public unsafe class SkyboxRenderer : WgpuRenderer<SkyboxParameters>
    {
        private readonly SlBuffer<float> _vertexBuffer;

        public SkyboxRenderer(SlDevice device)
            : base(device, CreateShader(device))
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
            // TODO: Clean this up
            var wgpu = (Device as WgpuDevice).Wgpu;
            wgpu.RenderPassEncoderSetPipeline(passEncoder, Shader.GetPipeline("default"));
            wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, (Buffer*)_vertexBuffer.GetHandle(), 0, _vertexBuffer.Size);
            wgpu.RenderPassEncoderDraw(passEncoder, 4, 1, 0, 0);
        }

        public override void Dispose()
        {
            _vertexBuffer.Dispose();

            base.Dispose();
        }
    }
}
