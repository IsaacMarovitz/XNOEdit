namespace Solaris.RHI
{
    public abstract class SlTexture : IDisposable
    {
        public abstract unsafe void* GetHandle();

        public abstract SlTextureView CreateTextureView(SlTextureViewDescriptor descriptor);

        public abstract void Dispose();
    }
}
