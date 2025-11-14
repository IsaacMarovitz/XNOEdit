using System.Numerics;
using Silk.NET.OpenGL;

namespace XNOEdit.Renderer
{
    public class SkyboxRenderer : IDisposable
    {
        private GL _gl;
        private XeShader _shader;
        private uint _vao;
        private uint _vbo;

        public SkyboxRenderer(GL gl)
        {
            _gl = gl;
            CreateQuad();
            CreateShader();
        }

        private void CreateQuad()
        {
            // Full-screen quad in NDC coordinates
            var vertices = new[]
            {
                -1.0f, -1.0f, 0.999f,  // Bottom-left (far depth)
                 1.0f, -1.0f, 0.999f,  // Bottom-right
                 1.0f,  1.0f, 0.999f,  // Top-right
                -1.0f,  1.0f, 0.999f   // Top-left
            };

            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (float* v = vertices)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
                }
            }

            unsafe
            {
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
                _gl.EnableVertexAttribArray(0);
            }

            _gl.BindVertexArray(0);
        }

        private void CreateShader()
        {
            _shader = new XeShader(_gl, "XNOEdit/Shaders/Skybox.vert", "XNOEdit/Shaders/Skybox.frag");
        }

        public void Draw(Matrix4x4 view, Matrix4x4 projection, Vector3 sunDirection, Vector3 sunColor)
        {
            // Disable depth writing so skybox is always behind everything
            _gl.DepthMask(false);

            _shader.Use();
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);
            _shader.SetUniform("uSunDirection", sunDirection);
            _shader.SetUniform("uSunColor", sunColor);

            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(GLEnum.TriangleFan, 0, 4);
            _gl.BindVertexArray(0);

            // Re-enable depth writing
            _gl.DepthMask(true);
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
