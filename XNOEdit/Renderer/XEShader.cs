using System.Numerics;
using Silk.NET.OpenGL;

namespace XNOEdit.Renderer
{
    public class XeShader : IDisposable
    {
        private readonly uint _handle;
        private readonly GL _gl;

        public XeShader(GL gl, string vertexPath, string fragmentPath)
        {
            _gl = gl;

            var vertex = LoadShader(ShaderType.VertexShader, EmbeddedResources.ReadAllText(vertexPath));
            var fragment = LoadShader(ShaderType.FragmentShader, EmbeddedResources.ReadAllText(fragmentPath));

            _handle = _gl.CreateProgram();
            _gl.AttachShader(_handle, vertex);
            _gl.AttachShader(_handle, fragment);
            _gl.LinkProgram(_handle);
            _gl.GetProgram(_handle, GLEnum.LinkStatus, out var status);

            if (status == 0)
            {
                throw new Exception($"Program failed to link with error: {_gl.GetProgramInfoLog(_handle)}");
            }

            _gl.DetachShader(_handle, vertex);
            _gl.DetachShader(_handle, fragment);
            _gl.DeleteShader(vertex);
            _gl.DeleteShader(fragment);
        }

        public void Use()
        {
            _gl.UseProgram(_handle);
        }

        public void SetUniform(string name, int value)
        {
            var location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
            _gl.Uniform1(location, value);
        }

        public void SetUniform(string name, float value)
        {
            var location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
            _gl.Uniform1(location, value);
        }

        public unsafe void SetUniform(string name, Matrix4x4 value)
        {
            var location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
            _gl.UniformMatrix4(location, 1, false, (float*)&value);
        }

        public void SetUniform(string name, Vector3 value)
        {
            var location = _gl.GetUniformLocation(_handle, name);
            if (location == -1)
            {
                throw new Exception($"{name} uniform not found on shader.");
            }
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }

        public void Dispose()
        {
            _gl.DeleteProgram(_handle);
        }

        private uint LoadShader(ShaderType type, string source)
        {
            var handle = _gl.CreateShader(type);
            _gl.ShaderSource(handle, source);
            _gl.CompileShader(handle);
            var infoLog = _gl.GetShaderInfoLog(handle);

            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                throw new Exception($"Error compiling shader of type {type}, failed with error {infoLog}");
            }

            return handle;
        }
    }
}
