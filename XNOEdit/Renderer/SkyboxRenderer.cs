using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Shaders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    public unsafe class SkyboxRenderer : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly WgpuShader<SkyboxUniforms> _shader;
        private readonly WgpuBuffer<float> _vertexBuffer;

        public SkyboxRenderer(WebGPU wgpu, Device* device, TextureFormat swapChainFormat)
        {
            _wgpu = wgpu;

            var vertices = new[]
            {
                -1.0f, -1.0f, 0.0f,
                1.0f,  -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f,   1.0f, 0.0f
            };
            _vertexBuffer = new WgpuBuffer<float>(wgpu, device, vertices, BufferUsage.Vertex);

            _shader = new SkyboxShader(
                wgpu,
                device,
                EmbeddedResources.ReadAllText("XNOEdit/Shaders/Skybox.wgsl"),
                swapChainFormat);
        }

        public void Draw(
            Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            Vector3 sunDirection,
            Vector3 sunColor)
        {
            var uniforms = new SkyboxUniforms
            {
                View = view,
                Projection = projection,
                SunDirection = sunDirection.AsVector4(),
                SunColor = sunColor.AsVector4()
            };

            _shader.UpdateUniforms(queue, in uniforms);
            _wgpu.RenderPassEncoderSetPipeline(passEncoder, _shader.GetPipeline());

            uint dynamicOffset = 0;
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, _shader.BindGroup, 0, &dynamicOffset);
            _wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            _wgpu.RenderPassEncoderDraw(passEncoder, 4, 1, 0, 0);
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _shader?.Dispose();
        }
    }
}
