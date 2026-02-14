namespace Solaris.RHI
{
    public abstract class SlSampler : IDisposable
    {
        public abstract unsafe void* GetHandle();

        public abstract void Dispose();
    }
}
