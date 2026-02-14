namespace Solaris
{
    public abstract class SlRenderPass : IDisposable
    {
        public abstract void SetPipeline(SlRenderPipeline pipeline);

        public abstract void SetVertexBuffer(uint slot, SlBuffer<byte> buffer, ulong offset = 0);

        // Generic overload so callers don't need to cast everything to SlBuffer<byte>
        public abstract void SetVertexBuffer<T>(uint slot, SlBuffer<T> buffer, ulong offset = 0) where T : unmanaged;

        public abstract void SetIndexBuffer(SlBuffer<byte> buffer, SlIndexFormat format, ulong offset = 0);

        public abstract void SetIndexBuffer<T>(SlBuffer<T> buffer, SlIndexFormat format, ulong offset = 0) where T : unmanaged;

        public abstract void SetBindGroup(uint index, SlBindGroup group);

        public abstract void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth);

        public abstract void SetScissorRect(uint x, uint y, uint width, uint height);

        public abstract void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);

        public abstract void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0);

        /// <summary>
        /// End the render pass. The pass should not be used after this call.
        /// </summary>
        public abstract void End();

        public abstract void Dispose();
    }
}
