namespace Solaris.RHI
{
    public abstract class SlQueue : IDisposable
    {
        public abstract void Submit(SlCommandBuffer commandBuffer);

        public abstract void Dispose();
    }
}
