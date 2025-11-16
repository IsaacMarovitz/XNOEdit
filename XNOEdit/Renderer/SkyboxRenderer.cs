using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Builders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SkyboxUniforms
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 SunDirection;
        public Vector4 SunColor;
    }

    public unsafe class SkyboxRenderer : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly WgpuShader<SkyboxUniforms> _shader;
        private readonly WgpuBuffer<float> _vertexBuffer;

        public SkyboxRenderer(WebGPU wgpu, Device* device, Queue* queue, TextureFormat swapChainFormat)
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

            var vertexAttrib = stackalloc VertexAttribute[1];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 };

            var vertexLayout = new VertexBufferLayout
            {
                ArrayStride = 12,
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 1,
                Attributes = vertexAttrib
            };

            var pipelineBuilder = new RenderPipelineBuilder(wgpu, device, swapChainFormat)
                .WithTopology(PrimitiveTopology.TriangleStrip)
                .WithDepth(write: false, compare: CompareFunction.Always);

            _shader = new WgpuShader<SkyboxUniforms>(
                wgpu,
                device,
                queue,
                EmbeddedResources.ReadAllText("XNOEdit/Shaders/Skybox.wgsl"),
                "Skybox",
                swapChainFormat,
                [vertexLayout],
                pipelineBuilder);
        }

        public void Draw(
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

            _shader.UpdateUniforms(in uniforms);
            _wgpu.RenderPassEncoderSetPipeline(passEncoder, _shader.GetPipeline());

            uint dynamicOffset = 0;
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, _shader.UniformBindGroup, 0, &dynamicOffset);
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
