namespace Solaris
{
    public abstract class SlBuffer<T> : ISlBuffer, IDisposable where T : unmanaged
    {
        public ulong Size { get; protected set; }

        public abstract ulong GpuAddress { get; }

        public abstract unsafe void* GetHandle();

        public abstract void UpdateData(SlQueue queue, Span<T> data, ulong offset = 0);

        public abstract void UpdateData(SlQueue queue, in T data, ulong offset = 0);

        public abstract void UpdateData(SlQueue queue, int index, in T data);

        public abstract void Dispose();
    }

    public interface ISlBuffer
    {
        ulong GpuAddress { get; }
    }
}
