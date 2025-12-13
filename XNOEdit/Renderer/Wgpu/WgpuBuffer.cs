using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer.Wgpu
{
    public unsafe class WgpuBuffer<T> : IDisposable where T : unmanaged
    {
        private readonly WebGPU _wgpu;

        public Buffer* Handle { get; }
        public ulong Size { get; }

        private const int CopyBufferAlignment = 4;
        private const int UniformBufferAlignment = 256;

        /// <summary>
        /// Create a buffer with initial data (for vertex/index buffers)
        /// </summary>
        public WgpuBuffer(WebGPU wgpu, Device* device, Span<T> data, BufferUsage usage)
        {
            _wgpu = wgpu;

            // Calculate size and align to COPY_BUFFER_ALIGNMENT
            var dataSize = (ulong)(data.Length * sizeof(T));
            Size = AlignUp(dataSize, CopyBufferAlignment);

            var descriptor = new BufferDescriptor
            {
                Size = Size,
                Usage = usage,
                MappedAtCreation = true
            };

            Handle = wgpu.DeviceCreateBuffer(device, &descriptor);

            // Write initial data
            var mappedRange = wgpu.BufferGetMappedRange(Handle, 0, (nuint)Size);
            var dataBytes = MemoryMarshal.AsBytes(data);
            dataBytes.CopyTo(new Span<byte>(mappedRange, (int)dataSize));
            wgpu.BufferUnmap(Handle);
        }

        /// <summary>
        /// Create an empty buffer (for uniform buffers that will be updated via QueueWriteBuffer)
        /// </summary>
        public WgpuBuffer(WebGPU wgpu, Device* device, BufferUsage usage, ulong size = 0, int alignment = CopyBufferAlignment)
        {
            _wgpu = wgpu;

            // Calculate size and align
            var dataSize = size == 0 ? (ulong)sizeof(T) : size;
            Size = AlignUp(dataSize, alignment);

            var descriptor = new BufferDescriptor
            {
                Size = Size,
                Usage = usage,
                MappedAtCreation = false
            };

            Handle = wgpu.DeviceCreateBuffer(device, &descriptor);
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
                (ulong)sizeof(T),
                UniformBufferAlignment);
        }

        public void UpdateData(Queue* queue, Span<T> data, ulong offset = 0)
        {
            var dataBytes = MemoryMarshal.AsBytes(data);
            fixed (byte* pData = dataBytes)
            {
                _wgpu.QueueWriteBuffer(queue, Handle, offset, pData, (nuint)dataBytes.Length);
            }
        }

        public void UpdateData(Queue* queue, in T data, ulong offset = 0)
        {
            fixed (T* pData = &data)
            {
                _wgpu.QueueWriteBuffer(queue, Handle, offset, pData, (nuint)sizeof(T));
            }
        }

        public void UpdateData(Queue* queue, int index, in T data)
        {
            var offset = (ulong)(index * sizeof(T));
            UpdateData(queue, in data, offset);
        }

        public BindGroupEntry CreateBindGroupEntry(uint binding, ulong offset = 0, ulong size = 0)
        {
            return new BindGroupEntry
            {
                Binding = binding,
                Buffer = Handle,
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
            if (Handle != null)
            {
                _wgpu.BufferDestroy(Handle);
                _wgpu.BufferRelease(Handle);
            }
        }
    }
}
