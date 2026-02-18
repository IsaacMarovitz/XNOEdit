namespace Solaris
{
    public abstract class SlTextureView : IDisposable
    {
        /// <summary>
        /// GPU resource identifier for bindless texture access (Metal 4).
        /// Returns 0 on backends that don't support resource IDs.
        /// </summary>
        public virtual ulong GpuResourceId => 0;

        public abstract unsafe void* GetHandle();

        public abstract void Dispose();
    }
}
