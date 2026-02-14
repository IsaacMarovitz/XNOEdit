namespace Solaris
{
    public abstract class SlCommandEncoder : IDisposable
    {
        /// <summary>
        /// Begin a render pass with the given attachments.
        /// </summary>
        public abstract SlRenderPass BeginRenderPass(SlRenderPassDescriptor descriptor);

        /// <summary>
        /// Finish encoding and produce a submittable command buffer.
        /// The encoder is consumed and should not be used after this call.
        /// </summary>
        public abstract SlCommandBuffer Finish();

        public abstract void Dispose();
    }
}
