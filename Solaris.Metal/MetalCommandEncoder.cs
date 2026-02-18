using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalCommandEncoder : SlCommandEncoder
    {
        private readonly MetalDevice _device;

        internal MetalCommandEncoder(MetalDevice device)
        {
            _device = device;
            device.BeginFrame();
        }

        public override unsafe SlRenderPass BeginRenderPass(SlRenderPassDescriptor descriptor)
        {
            var passDesc = new MTL4RenderPassDescriptor();

            if (descriptor.ColorAttachments != null)
            {
                for (var i = 0; i < descriptor.ColorAttachments.Length; i++)
                {
                    ref var src = ref descriptor.ColorAttachments[i];
                    var mtlTexture = new MTLTexture((IntPtr)src.View.GetHandle());

                    var attachment = passDesc.ColorAttachments.Object((ulong)i);
                    attachment.Texture = mtlTexture;
                    attachment.LoadAction = src.LoadOp.Convert();
                    attachment.StoreAction = src.StoreOp.Convert();
                    attachment.ClearColor = new MTLClearColor
                    {
                        red = src.ClearValue.R,
                        green = src.ClearValue.G,
                        blue = src.ClearValue.B,
                        alpha = src.ClearValue.A,
                    };
                }
            }

            if (descriptor.DepthStencilAttachment.HasValue)
            {
                var depthAttachment = passDesc.DepthAttachment;
                var ds = descriptor.DepthStencilAttachment.Value;

                depthAttachment.Texture = (MetalTextureView)ds.View;
                depthAttachment.LoadAction = ds.DepthLoadOp.Convert();
                depthAttachment.StoreAction = ds.DepthStoreOp.Convert();
                depthAttachment.ClearDepth = ds.DepthClearValue;

                passDesc.DepthAttachment =  depthAttachment;
            }

            _device.CommitResidency();

            var encoder = _device.CommandBuffer.RenderCommandEncoder(passDesc);
            passDesc.Dispose();

            return new MetalRenderPass(_device, encoder);
        }

        public override SlCommandBuffer Finish()
        {
            _device.CommandBuffer.EndCommandBuffer();
            return new MetalCommandBuffer(_device);
        }

        public override void Dispose() { }
    }
}
