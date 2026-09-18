using System.Numerics;
using Solaris;
using Solaris.Graph;
using XNOEdit.Renderer.Shaders;

namespace XNOEdit.Renderer.Renderers
{
    public struct GridParameters
    {
        public Matrix4x4 Model;
        public Vector3 Position;
        public float FadeDistance;
    }

    public class GridRenderer : Renderer<GridParameters>
    {
        private readonly SlBuffer _vertexBuffer;
        private readonly int _lineCount;

        public GridRenderer(SlDevice device, float size = 100.0f, int divisions = 100)
            : base(CreateShader(device))
        {
            var vertices = CreateGridVertices(size, divisions);
            _lineCount = (divisions + 1) * 2 * 2;
            _vertexBuffer = device.CreateBuffer(vertices, SlBufferUsage.Vertex);
        }

        private static GridShader CreateShader(SlDevice device)
        {
            return new GridShader(device,
                ShaderLibrary.Get(device, "grid_vs"), ShaderLibrary.Get(device, "grid_ps"));
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
            SlPassContext ctx,
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

            var offset = ctx.UploadConstants(in uniforms);

            ctx.SetPipeline(Material.Pipeline(ctx.Signature));
            ctx.PushConstants(in offset);
            ctx.SetVertexBuffer(0, _vertexBuffer.View, stride: 24);
            ctx.Draw((uint)_lineCount);
        }

        public override void Dispose()
        {
            _vertexBuffer.Dispose();

            base.Dispose();
        }
    }
}
