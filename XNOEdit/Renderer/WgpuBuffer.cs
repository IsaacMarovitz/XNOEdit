using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer
{
    public unsafe class WgpuBuffer<T> : IDisposable where T : unmanaged
    {
        private readonly WebGPU _wgpu;
        private readonly Buffer* _buffer;
        private readonly BufferUsage _usage;
        private readonly ulong _size;

        public Buffer* Handle => _buffer;
        public ulong Size => _size;

        private const int COPY_BUFFER_ALIGNMENT = 4;

        public WgpuBuffer(WebGPU wgpu, Device* device, Span<T> data, BufferUsage usage)
        {
            _wgpu = wgpu;
            _usage = usage;

            // Calculate size and align to COPY_BUFFER_ALIGNMENT
            var dataSize = (ulong)(data.Length * sizeof(T));
            _size = AlignUp(dataSize, COPY_BUFFER_ALIGNMENT);

            var descriptor = new BufferDescriptor
            {
                Size = _size,
                Usage = usage,
                MappedAtCreation = true
            };

            _buffer = wgpu.DeviceCreateBuffer(device, &descriptor);

            // Write initial data
            var mappedRange = wgpu.BufferGetMappedRange(_buffer, 0, (uint)_size);
            var dataBytes = MemoryMarshal.AsBytes(data);
            dataBytes.CopyTo(new Span<byte>(mappedRange, (int)dataSize));
            wgpu.BufferUnmap(_buffer);
        }

        public void UpdateData(Queue* queue, Span<T> data, ulong offset = 0)
        {
            var dataBytes = MemoryMarshal.AsBytes(data);
            fixed (byte* pData = dataBytes)
            {
                _wgpu.QueueWriteBuffer(queue, _buffer, offset, pData, (nuint)dataBytes.Length);
            }
        }

        private static ulong AlignUp(ulong value, int alignment)
        {
            return (value + (ulong)alignment - 1) & ~((ulong)alignment - 1);
        }

        public void Dispose()
        {
            if (_buffer != null)
            {
                _wgpu.BufferRelease(_buffer);
            }
        }
    }
}
