using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Shaders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    public unsafe class GridRenderer : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly WgpuShader<GridUniforms> _shader;
        private readonly WgpuBuffer<float> _vertexBuffer;
        private readonly int _lineCount;

        public GridRenderer(WebGPU wgpu, Device* device, Queue* queue, TextureFormat swapChainFormat, float size = 100.0f, int divisions = 100)
        {
            _wgpu = wgpu;

            var vertices = CreateGridVertices(size, divisions);
            _lineCount = (divisions + 1) * 2 * 2;
            _vertexBuffer = new WgpuBuffer<float>(wgpu, device, vertices, BufferUsage.Vertex);

            _shader = new GridShader(
                wgpu,
                device,
                queue,
                EmbeddedResources.ReadAllText("XNOEdit/Shaders/Grid.wgsl"),
                swapChainFormat);
        }

        private float[] CreateGridVertices(float size, int divisions)
        {
            var vertices = new List<float>();
            var step = size / divisions;
            var halfSize = size / 2.0f;

            for (var i = 0; i <= divisions; i++)
            {
                var z = -halfSize + i * step;
                var color = i == divisions / 2 ? new Vector3(0.4f, 0.6f, 1.0f)
                    : i % 10 == 0 ? new Vector3(0.5f, 0.5f, 0.5f)
                    : new Vector3(0.3f, 0.3f, 0.3f);

                vertices.AddRange([-halfSize, 0.0f, z, color.X, color.Y, color.Z]);
                vertices.AddRange([halfSize, 0.0f, z, color.X, color.Y, color.Z]);
            }

            for (var i = 0; i <= divisions; i++)
            {
                var x = -halfSize + i * step;
                var color = i == divisions / 2 ? new Vector3(1.0f, 0.4f, 0.4f)
                    : i % 10 == 0 ? new(0.5f, 0.5f, 0.5f)
                    : new Vector3(0.3f, 0.3f, 0.3f);

                vertices.AddRange([x, 0.0f, -halfSize, color.X, color.Y, color.Z]);
                vertices.AddRange([x, 0.0f, halfSize, color.X, color.Y, color.Z]);
            }

            return vertices.ToArray();
        }

        public void Draw(
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            Matrix4x4 model,
            Vector3 cameraPos,
            float fadeDistance)
        {
            var uniforms = new GridUniforms
            {
                Model = model,
                View = view,
                Projection = projection,
                CameraPos = cameraPos,
                FadeStart = fadeDistance * 0.6f,
                FadeEnd = fadeDistance
            };

            _shader.UpdateUniforms(in uniforms);
            _wgpu.RenderPassEncoderSetPipeline(passEncoder, _shader.GetPipeline());

            uint dynamicOffset = 0;
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, _shader.BindGroup, 0, &dynamicOffset);
            _wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            _wgpu.RenderPassEncoderDraw(passEncoder, (uint)_lineCount, 1, 0, 0);
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _shader?.Dispose();
        }
    }
}
