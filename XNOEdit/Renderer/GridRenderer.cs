using System.Numerics;
using Silk.NET.OpenGL;

namespace XNOEdit.Renderer
{
    public class GridRenderer : IDisposable
    {
        private GL _gl;
        private XeShader _shader;
        private uint _vao;
        private uint _vbo;
        private int _lineCount;

        public GridRenderer(GL gl, float size = 100.0f, int divisions = 100)
        {
            _gl = gl;
            CreateGrid(size, divisions);
            CreateShader();
        }

        private void CreateGrid(float size, int divisions)
        {
            var vertices = new List<float>();

            float step = size / divisions;
            float halfSize = size / 2.0f;

            // Create grid lines parallel to X axis
            for (int i = 0; i <= divisions; i++)
            {
                float z = -halfSize + i * step;

                // Determine color
                Vector3 color;
                if (i == divisions / 2)
                {
                    color = new Vector3(0.4f, 0.6f, 1.0f); // Bright blue Z-axis
                }
                else if (i % 10 == 0)
                {
                    color = new Vector3(0.5f, 0.5f, 0.5f); // Medium gray - brighter
                }
                else
                {
                    color = new Vector3(0.3f, 0.3f, 0.3f); // Light gray regular lines
                }

                // Line vertices: start and end
                vertices.AddRange(new[] { -halfSize, 0.0f, z, color.X, color.Y, color.Z });
                vertices.AddRange(new[] { halfSize, 0.0f, z, color.X, color.Y, color.Z });
            }

            // Create grid lines parallel to Z axis
            for (int i = 0; i <= divisions; i++)
            {
                float x = -halfSize + i * step;

                // Determine color
                Vector3 color;
                if (i == divisions / 2)
                {
                    color = new Vector3(1.0f, 0.4f, 0.4f); // Bright red X-axis
                }
                else if (i % 10 == 0)
                {
                    color = new Vector3(0.5f, 0.5f, 0.5f); // Medium gray - brighter
                }
                else
                {
                    color = new Vector3(0.3f, 0.3f, 0.3f); // Light gray regular lines
                }

                // Line vertices: start and end
                vertices.AddRange(new[] { x, 0.0f, -halfSize, color.X, color.Y, color.Z });
                vertices.AddRange(new[] { x, 0.0f, halfSize, color.X, color.Y, color.Z });
            }

            _lineCount = (divisions + 1) * 2 * 2; // 2 sets of lines, 2 vertices per line

            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                var vertArray = vertices.ToArray();
                fixed (float* v = vertArray)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertArray.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
                }
            }

            unsafe
            {
                // Position attribute
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
                _gl.EnableVertexAttribArray(0);

                // Color attribute
                _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
                _gl.EnableVertexAttribArray(1);
            }

            _gl.BindVertexArray(0);
        }

        private void CreateShader()
        {
            _shader = new XeShader(_gl, "XNOEdit/Shaders/Grid.vert", "XNOEdit/Shaders/Grid.frag");
        }

        public unsafe void Draw(Matrix4x4 view, Matrix4x4 projection, Matrix4x4 model, Vector3 cameraPos, float fadeDistance)
        {
            // Enable blending for faded lines
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _shader.Use();
            _shader.SetUniform("uModel", model);
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);
            _shader.SetUniform("uCameraPos", cameraPos);
            _shader.SetUniform("uFadeStart", fadeDistance * 0.6f);
            _shader.SetUniform("uFadeEnd", fadeDistance);

            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(GLEnum.Lines, 0, (uint)_lineCount);
            _gl.BindVertexArray(0);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            if (_vao != 0)
                _gl.DeleteVertexArray(_vao);
            if (_vbo != 0)
                _gl.DeleteBuffer(_vbo);
        }
    }
}

