namespace Solaris.RHI
{
    public abstract class SlQueue : IDisposable
    {
        public abstract void WriteTexture(SlCopyTextureDescriptor descriptor, Span<byte> data, SlTextureDataLayout layout, SlExtent3D extent);

        public abstract unsafe void WriteTexture(SlCopyTextureDescriptor descriptor, byte* data, nuint size, SlTextureDataLayout layout, SlExtent3D extent);

        public abstract void Submit(SlCommandBuffer commandBuffer);

        public abstract void Dispose();
    }
}
