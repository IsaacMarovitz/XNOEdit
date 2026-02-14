namespace Solaris.RHI
{
    public abstract class SlBuffer<T> : IDisposable where T : unmanaged
    {
        public ulong Size { get; protected set; }

        public abstract unsafe void* GetHandle();

        public abstract void UpdateData(SlQueue queue, Span<T> data, ulong offset = 0);

        public abstract void UpdateData(SlQueue queue, in T data, ulong offset = 0);

        public abstract void UpdateData(SlQueue queue, int index, in T data);

        public abstract void Dispose();
    }
}
