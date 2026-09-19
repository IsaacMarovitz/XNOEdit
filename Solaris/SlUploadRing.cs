using System.Runtime.InteropServices;
using Plume;

namespace Solaris
{
    /// <summary>
    /// A per-frame linear allocator over one persistently mapped upload buffer.
    ///
    /// The block is fixed size rather than growable. Exhausting the block throws.
    /// </summary>
    public sealed unsafe class SlUploadRing : IDisposable
    {
        /// <summary>Stride of the structured buffer the shader sees.</summary>
        public const ulong ConstantElementSize = 16;

        private readonly SlBuffer _block;
        private readonly ulong _capacity;

        private RenderDescriptorSet* _set;
        private ulong _cursor;
        private ulong _highWaterMark;
        private bool _disposed;

        internal SlUploadRing(SlDevice device, ulong capacity)
        {
            _capacity = capacity;

            _block = device.CreateBuffer(
                capacity,
                SlBufferUsage.Upload | SlBufferUsage.Vertex | SlBufferUsage.Index |
                SlBufferUsage.Constant | SlBufferUsage.Structured,
                "UploadRing");

            // Written once at construction. The buffer never changes, so this set is
            // never mutated and can safely be bound while earlier frames are in flight.
            var desc = device.Layout.ConstantSetDesc;
            _set = device.Handle->CreateDescriptorSet(&desc);

            if (_set == null)
                throw new InvalidOperationException("Failed to allocate the upload ring's descriptor set.");

            var structuredView = new RenderBufferStructuredView((uint)ConstantElementSize);
            _set->SetBuffer(0, _block.Handle, capacity, &structuredView);
        }

        /// <summary>The set bound at <see cref="SlGlobalLayout.ConstantSet"/> for every pass.</summary>
        internal RenderDescriptorSet* DescriptorSet => _set;

        public ulong Capacity => _capacity;

        public ulong BytesUsedThisFrame => _cursor;

        /// <summary>Largest single-frame usage seen, for sizing the block sensibly.</summary>
        public ulong HighWaterMark => _highWaterMark;

        /// <summary>Called by the frame graph when this ring's frame slot comes around again.</summary>
        internal void Reset()
        {
            if (_cursor > _highWaterMark)
            {
                _highWaterMark = _cursor;
            }

            _cursor = 0;
        }

        /// <summary>
        /// Reserves <paramref name="sizeInBytes"/> and returns the writable span along
        /// with a view describing where it landed.
        /// </summary>
        public SlAllocation Allocate(ulong sizeInBytes, ulong alignment = ConstantElementSize)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var aligned = (_cursor + alignment - 1) & ~(alignment - 1);

            if (aligned + sizeInBytes > _capacity)
            {
                throw new InvalidOperationException(
                    $"The upload ring is full: {aligned + sizeInBytes} bytes needed of {_capacity} available. " +
                    "Raise SlFrameGraph.RingBlockSize or reduce per-frame transient data.");
            }

            _cursor = aligned + sizeInBytes;

            var span = _block.AsSpan().Slice(checked((int)aligned), checked((int)sizeInBytes));
            return new SlAllocation(_block.Slice(aligned, sizeInBytes), span, aligned);
        }

        /// <summary>
        /// Stages a constant block. The returned <see cref="SlAllocation.ConstantOffset"/>
        /// is what a shader pushes and indexes the structured buffer with, so the data is
        /// always aligned to an element boundary.
        /// </summary>
        public SlAllocation Write<T>(in T value) where T : unmanaged
        {
            var allocation = Allocate((ulong)Marshal.SizeOf<T>(), ConstantElementSize);
            MemoryMarshal.Write(allocation.Data, in value);
            return allocation;
        }

        public SlAllocation Write<T>(ReadOnlySpan<T> values, ulong alignment = ConstantElementSize) where T : unmanaged
        {
            var allocation = Allocate((ulong)(values.Length * Marshal.SizeOf<T>()), alignment);
            MemoryMarshal.Cast<T, byte>(values).CopyTo(allocation.Data);
            return allocation;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_set != null)
            {
                _set->Dispose();
                _set = null;
            }

            _block.Dispose();
        }
    }

    /// <summary>
    /// A reserved range in the upload ring. <see cref="Data"/> is only valid until the
    /// frame it was allocated in retires, so never store one across frames.
    /// </summary>
    public readonly ref struct SlAllocation(SlBufferView view, Span<byte> data, ulong byteOffset)
    {
        public readonly SlBufferView View = view;
        public readonly Span<byte> Data = data;

        /// <summary>Byte offset within the ring buffer, for vertex and index binding.</summary>
        public readonly ulong ByteOffset = byteOffset;

        /// <summary>
        /// Offset in 16-byte elements, which is how a shader indexes the constant
        /// structured buffer. This is the value to push.
        /// </summary>
        public uint ConstantOffset => checked((uint)(ByteOffset / SlUploadRing.ConstantElementSize));
    }
}
