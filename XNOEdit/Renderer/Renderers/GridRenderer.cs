using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Shaders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer.Renderers
{
    public struct GridParameters
    {
        public Matrix4x4 Model;
        public Vector3 Position;
        public float FadeDistance;
    }

    public unsafe class GridRenderer : WgpuRenderer<GridParameters>
    {
        private readonly WgpuBuffer<float> _vertexBuffer;
        private readonly int _lineCount;

        public GridRenderer(WebGPU wgpu, WgpuDevice device, float size = 100.0f, int divisions = 100)
            : base(wgpu, CreateShader(wgpu, device))
        {
            var vertices = CreateGridVertices(size, divisions);
            _lineCount = (divisions + 1) * 2 * 2;
            _vertexBuffer = new WgpuBuffer<float>(wgpu, device, vertices, BufferUsage.Vertex);
        }

        private static GridShader CreateShader(WebGPU wgpu, WgpuDevice device)
        {
            return new GridShader(wgpu, device, EmbeddedResources.ReadAllText("XNOEdit/Shaders/Grid.wgsl"));
        }

        private static float[] CreateGridVertices(float size, int divisions)
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

        public override void Draw(
            Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            GridParameters gridParameters)
        {
            var uniforms = new GridUniforms
            {
                Model = gridParameters.Model,
                View = view,
                Projection = projection,
                CameraPos = gridParameters.Position,
                FadeStart = gridParameters.FadeDistance * 0.6f,
                FadeEnd = gridParameters.FadeDistance
            };

            ((GridShader)Shader).UpdateUniforms(queue, in uniforms);
            Wgpu.RenderPassEncoderSetPipeline(passEncoder, Shader.GetPipeline());

            uint dynamicOffset = 0;
            Wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, Shader.BindGroup, 0, &dynamicOffset);
            Wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            Wgpu.RenderPassEncoderDraw(passEncoder, (uint)_lineCount, 1, 0, 0);
        }

        public override void Dispose()
        {
            _vertexBuffer?.Dispose();

            base.Dispose();
        }
    }
}
