namespace Solaris
{
    public abstract class SlTexture : IDisposable
    {
        public abstract SlTextureView CreateTextureView(SlTextureViewDescriptor descriptor);

        public abstract void Dispose();
    }
}
