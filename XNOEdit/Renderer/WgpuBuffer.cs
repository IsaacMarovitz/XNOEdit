using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer
{
    public unsafe class WgpuBuffer<T> : IDisposable where T : struct
    {
        private readonly WebGPU _wgpu;
        private readonly Buffer* _buffer;
        private readonly BufferUsage _usage;
        private readonly ulong _size;

        public Buffer* Handle => _buffer;
        public ulong Size => _size;

        private const int CopyBufferAlignment = 4;
        private const int UniformBufferAlignment = 256;

        /// <summary>
        /// Create a buffer with initial data (for vertex/index buffers)
        /// </summary>
        public WgpuBuffer(WebGPU wgpu, Device* device, Span<T> data, BufferUsage usage)
        {
            _wgpu = wgpu;
            _usage = usage;

            // Calculate size and align to COPY_BUFFER_ALIGNMENT
            var dataSize = (ulong)(data.Length * sizeof(T));
            _size = AlignUp(dataSize, CopyBufferAlignment);

            var descriptor = new BufferDescriptor
            {
                Size = _size,
                Usage = usage,
                MappedAtCreation = true
            };

            _buffer = wgpu.DeviceCreateBuffer(device, &descriptor);

            // Write initial data
            var mappedRange = wgpu.BufferGetMappedRange(_buffer, 0, (nuint)_size);
            var dataBytes = MemoryMarshal.AsBytes(data);
            dataBytes.CopyTo(new Span<byte>(mappedRange, (int)dataSize));
            wgpu.BufferUnmap(_buffer);
        }

        /// <summary>
        /// Create an empty buffer (for uniform buffers that will be updated via QueueWriteBuffer)
        /// </summary>
        public WgpuBuffer(WebGPU wgpu, Device* device, BufferUsage usage, ulong size = 0, int alignment = CopyBufferAlignment)
        {
            _wgpu = wgpu;
            _usage = usage;

            // Calculate size and align
            var dataSize = size == 0 ? (ulong)sizeof(T) : size;
            _size = AlignUp(dataSize, alignment);

            var descriptor = new BufferDescriptor
            {
                Size = _size,
                Usage = usage,
                MappedAtCreation = false
            };

            _buffer = wgpu.DeviceCreateBuffer(device, &descriptor);
        }

        /// <summary>
        /// Create a uniform buffer (convenience method)
        /// </summary>
        public static WgpuBuffer<T> CreateUniform(WebGPU wgpu, Device* device)
        {
            return new WgpuBuffer<T>(
                wgpu,
                device,
                BufferUsage.Uniform | BufferUsage.CopyDst,
                UniformBufferAlignment);
        }

        public void UpdateData(Queue* queue, Span<T> data, ulong offset = 0)
        {
            var dataBytes = MemoryMarshal.AsBytes(data);
            fixed (byte* pData = dataBytes)
            {
                _wgpu.QueueWriteBuffer(queue, _buffer, offset, pData, (nuint)dataBytes.Length);
            }
        }

        public void UpdateData(Queue* queue, in T data, ulong offset = 0)
        {
            fixed (T* pData = &data)
            {
                _wgpu.QueueWriteBuffer(queue, _buffer, offset, pData, (nuint)sizeof(T));
            }
        }

        public BindGroupEntry CreateBindGroupEntry(uint binding, ulong offset = 0, ulong size = 0)
        {
            return new BindGroupEntry
            {
                Binding = binding,
                Buffer = _buffer,
                Offset = offset,
                Size = size == 0 ? (ulong)sizeof(T) : size
            };
        }

        public static BindGroupLayoutEntry CreateLayoutEntry(
            uint binding,
            ShaderStage visibility,
            BufferBindingType type = BufferBindingType.Uniform)
        {
            return new BindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Buffer = new BufferBindingLayout
                {
                    Type = type,
                    MinBindingSize = 0
                }
            };
        }

        private static ulong AlignUp(ulong value, int alignment)
        {
            return (value + (ulong)alignment - 1) & ~((ulong)alignment - 1);
        }

        public void Dispose()
        {
            if (_buffer != null)
            {
                _wgpu.BufferDestroy(_buffer);
                _wgpu.BufferRelease(_buffer);
            }
        }
    }
}
