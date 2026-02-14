using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Solaris.RHI;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Solaris.Wgpu
{
    public unsafe class WgpuBuffer<T> : SlBuffer<T> where T : unmanaged
    {
        private readonly WebGPU _wgpu;

        public Buffer* Handle { get; }

        internal WgpuBuffer(Buffer* handle, ulong size, WebGPU wgpu)
        {
            Handle = handle;
            Size = size;
            _wgpu = wgpu;
        }

        public override void* GetHandle()
        {
            return Handle;
        }

        public override void UpdateData(SlQueue queue, Span<T> data, ulong offset = 0)
        {
            var wgpuQueue = (queue as WgpuQueue).Queue;
            var dataBytes = MemoryMarshal.AsBytes(data);
            fixed (byte* pData = dataBytes)
            {
                _wgpu.QueueWriteBuffer(wgpuQueue, Handle, offset, pData, (nuint)dataBytes.Length);
            }
        }

        public override void UpdateData(SlQueue queue, in T data, ulong offset = 0)
        {
            var wgpuQueue = (queue as WgpuQueue).Queue;
            fixed (T* pData = &data)
            {
                _wgpu.QueueWriteBuffer(wgpuQueue, Handle, offset, pData, (nuint)sizeof(T));
            }
        }

        public override void UpdateData(SlQueue queue, int index, in T data)
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

        public override void Dispose()
        {
            if (Handle != null)
            {
                _wgpu.BufferDestroy(Handle);
                _wgpu.BufferRelease(Handle);
            }
        }
    }
}
