using System.Runtime.InteropServices;
using System.Text;
using Plume;

namespace Solaris
{
    public sealed unsafe class SlShader : IDisposable
    {
        private RenderShader* _shader;
        private void* _bytecode;

        internal SlShader(RenderShader* shader, void* bytecode, RenderShaderFormat format)
        {
            _shader = shader;
            _bytecode = bytecode;
            Format = format;
        }

        public RenderShaderFormat Format { get; }

        internal RenderShader* Handle => _shader;


        public static SlShader Create(SlDevice device, ReadOnlySpan<byte> bytecode, string entryPoint)
        {
            ArgumentNullException.ThrowIfNull(device);

            if (bytecode.IsEmpty)
                throw new ArgumentException("Shader bytecode is empty.", nameof(bytecode));

            // Plume's Metal backend wraps this pointer in a dispatch_data with a no-op
            // destructor, so it never copies, the buffer has to outlive the shader.
            // A pinned managed array is not enough, since the GC can still move it.
            var storage = NativeMemory.Alloc((nuint)bytecode.Length);
            bytecode.CopyTo(new Span<byte>(storage, bytecode.Length));

            var nameBytes = Encoding.UTF8.GetBytes(entryPoint);
            var nameBuffer = new byte[nameBytes.Length + 1];
            nameBytes.CopyTo(nameBuffer, 0);

            fixed (byte* name = nameBuffer)
            {
                var format = device.Capabilities.ShaderFormat;
                var handle = device.Handle->CreateShader(storage, (ulong)bytecode.Length, (sbyte*)name, format);

                if (handle == null)
                {
                    NativeMemory.Free(storage);
                    throw new InvalidOperationException($"Failed to create the '{entryPoint}' shader.");
                }

                return new SlShader(handle, storage, format);
            }
        }

        public void Dispose()
        {
            if (_shader != null)
            {
                _shader->Dispose();
                _shader = null;
            }

            if (_bytecode != null)
            {
                NativeMemory.Free(_bytecode);
                _bytecode = null;
            }
        }
    }
}
