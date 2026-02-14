namespace Solaris
{
    public abstract class SlTextureView : IDisposable
    {
        public abstract unsafe void* GetHandle();

        public abstract void Dispose();
    }
}
