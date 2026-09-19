using System.Runtime.InteropServices;
using Plume;

namespace Solaris
{
    [Flags]
    public enum SlBufferUsage : uint
    {
        None = 0,
        Vertex = 1 << 0,
        Index = 1 << 1,
        Constant = 1 << 2,
        Structured = 1 << 3,

        /// <summary>CPU-writable. Implies an upload heap rather than device-local memory.</summary>
        Upload = 1 << 16,
    }

    public sealed unsafe class SlBuffer : IDisposable
    {
        private RenderBuffer* _buffer;
        private byte* _mapped;
        private bool _disposed;

        internal SlBuffer(RenderBuffer* buffer, ulong sizeInBytes, SlBufferUsage usage)
        {
            _buffer = buffer;
            SizeInBytes = sizeInBytes;
            Usage = usage;
        }

        public ulong SizeInBytes { get; }

        public SlBufferUsage Usage { get; }

        public bool IsUpload => (Usage & SlBufferUsage.Upload) != 0;

        internal RenderBuffer* Handle => _buffer;

        /// <summary>A view over the whole buffer.</summary>
        public SlBufferView View => new(this, 0, SizeInBytes);

        public SlBufferView Slice(ulong offset, ulong size)
        {
            if (offset + size > SizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(size), "Slice exceeds buffer bounds.");

            return new SlBufferView(this, offset, size);
        }

        /// <summary>
        /// Persistently maps an upload buffer and returns the whole range as bytes. The
        /// mapping is kept for the buffer's lifetime.
        /// </summary>
        public Span<byte> AsSpan()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsUpload)
                throw new InvalidOperationException("Only upload buffers can be mapped. Use SlDevice.Upload for device-local buffers.");

            if (_mapped == null)
            {
                _mapped = (byte*)_buffer->Map();

                if (_mapped == null)
                    throw new InvalidOperationException("Mapping the buffer returned null.");
            }

            return new Span<byte>(_mapped, checked((int)SizeInBytes));
        }

        public Span<T> AsSpan<T>() where T : unmanaged => MemoryMarshal.Cast<byte, T>(AsSpan());

        /// <summary>Writes at an element offset, in units of <typeparamref name="T"/>.</summary>
        public void Write<T>(ReadOnlySpan<T> source, int elementOffset = 0) where T : unmanaged
        {
            var destination = AsSpan<T>();

            if (elementOffset + source.Length > destination.Length)
                throw new ArgumentOutOfRangeException(nameof(source), "Write exceeds buffer bounds.");

            source.CopyTo(destination[elementOffset..]);
        }

        public void Write<T>(in T value, int elementOffset = 0) where T : unmanaged
        {
            Write(new ReadOnlySpan<T>(in value), elementOffset);
        }

        public void SetName(string name)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            SlUtf8.WithPointer(name, ptr => _buffer->SetName(ptr));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_mapped != null)
            {
                _buffer->Unmap();
                _mapped = null;
            }

            _buffer->Dispose();
            _buffer = null;
        }
    }

    /// <summary>
    /// A bindable range of a buffer. Passed by value; holds a managed reference so the
    /// buffer cannot be collected while a view exists.
    /// </summary>
    public readonly struct SlBufferView(SlBuffer buffer, ulong offset, ulong size)
    {
        public readonly SlBuffer Buffer = buffer;
        public readonly ulong Offset = offset;
        public readonly ulong Size = size;

        public bool IsEmpty => Buffer == null || Size == 0;

        internal unsafe RenderBufferReference Reference =>
            new(Buffer.Handle, Offset);
    }
}
