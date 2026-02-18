using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal unsafe class MetalBuffer<T> : SlBuffer<T> where T : unmanaged
    {
        private const int MaxFramesInFlight = 3;

        private readonly MetalDevice _device;
        private readonly MTLBuffer[] _handles;
        private readonly bool _isTripleBuffered;

        public static implicit operator MTLBuffer(MetalBuffer<T> buffer)
            => buffer._handles[buffer.CurrentIndex];

        private int CurrentIndex => _isTripleBuffered ? _device.CurrentFrameIndex : 0;

        public override ulong GpuAddress => _handles[CurrentIndex].GpuAddress;

        internal MetalBuffer(MetalDevice device, Span<T> data, SlBufferUsage usage)
        {
            _device = device;
            Size = (ulong)(data.Length * sizeof(T));

            _isTripleBuffered = usage.HasFlag(SlBufferUsage.Uniform);
            var bufferCount = _isTripleBuffered ? MaxFramesInFlight : 1;
            _handles = new MTLBuffer[bufferCount];

            fixed (T* ptr = data)
            {
                for (int i = 0; i < bufferCount; i++)
                {
                    _handles[i] = device.Device.NewBuffer((IntPtr)ptr, Size, MTLResourceOptions.ResourceStorageModeShared);
                    device.MakeResident(_handles[i]);
                }
            }
        }

        internal MetalBuffer(MetalDevice device, SlBufferDescriptor descriptor)
        {
            _device = device;

            var alignedSize = descriptor.Alignment > 0
                ? AlignUp(descriptor.Size, (ulong)descriptor.Alignment)
                : descriptor.Size;

            Size = alignedSize;

            _isTripleBuffered = descriptor.Usage.HasFlag(SlBufferUsage.Uniform);
            var bufferCount = _isTripleBuffered ? MaxFramesInFlight : 1;
            _handles = new MTLBuffer[bufferCount];

            for (int i = 0; i < bufferCount; i++)
            {
                _handles[i] = device.Device.NewBuffer((UIntPtr)alignedSize, MTLResourceOptions.ResourceStorageModeShared);
                device.MakeResident(_handles[i]);
            }
        }

        public override void* GetHandle() => (void*)_handles[CurrentIndex].NativePtr;

        public override void UpdateData(SlQueue queue, Span<T> data, ulong offset = 0)
        {
            var byteLength = (ulong)(data.Length * sizeof(T));

            fixed (T* src = data)
            {
                if (_isTripleBuffered)
                {
                    // Write to all buffers to ensure static data is replicated
                    for (int i = 0; i < _handles.Length; i++)
                    {
                        var dest = (byte*)_handles[i].Contents + offset;
                        Buffer.MemoryCopy(src, dest, Size - offset, byteLength);
                    }
                }
                else
                {
                    var dest = (byte*)_handles[0].Contents + offset;
                    Buffer.MemoryCopy(src, dest, Size - offset, byteLength);
                }
            }
        }

        public override void UpdateData(SlQueue queue, in T data, ulong offset = 0)
        {
            fixed (T* src = &data)
            {
                if (_isTripleBuffered)
                {
                    for (int i = 0; i < _handles.Length; i++)
                    {
                        var dest = (byte*)_handles[i].Contents + offset;
                        Buffer.MemoryCopy(src, dest, Size - offset, (ulong)sizeof(T));
                    }
                }
                else
                {
                    var dest = (byte*)_handles[0].Contents + offset;
                    Buffer.MemoryCopy(src, dest, Size - offset, (ulong)sizeof(T));
                }
            }
        }

        public override void UpdateData(SlQueue queue, int index, in T data)
        {
            UpdateData(queue, in data, (ulong)(index * sizeof(T)));
        }

        public override void Dispose()
        {
            foreach (var handle in _handles)
            {
                if (handle.NativePtr != IntPtr.Zero)
                {
                    _device.RemoveResident(handle);
                    handle.SetPurgeableState(MTLPurgeableState.Empty);
                    handle.Dispose();
                }
            }
        }

        private static ulong AlignUp(ulong value, ulong alignment)
            => (value + alignment - 1) & ~(alignment - 1);
    }
}
