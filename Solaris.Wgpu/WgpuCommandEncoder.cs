using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuCommandEncoder : SlCommandEncoder
    {
        private readonly WgpuDevice _device;
        private readonly CommandEncoder* _handle;

        public static implicit operator CommandEncoder*(WgpuCommandEncoder encoder) => encoder._handle;

        public WgpuCommandEncoder(WgpuDevice device)
        {
            _device = device;

            var desc = new CommandEncoderDescriptor();
            _handle = _device.Wgpu.DeviceCreateCommandEncoder(_device, &desc);
        }

        public override SlRenderPass BeginRenderPass(SlRenderPassDescriptor descriptor)
        {
            var colorCount = descriptor.ColorAttachments?.Length ?? 0;
            var colorAttachments = stackalloc RenderPassColorAttachment[colorCount];

            for (var i = 0; i < colorCount; i++)
            {
                ref var src = ref descriptor.ColorAttachments![i];
                var wgpuView = (WgpuTextureView)src.View;

                colorAttachments[i] = new RenderPassColorAttachment
                {
                    View = wgpuView,
                    LoadOp = src.LoadOp.Convert(),
                    StoreOp = src.StoreOp.Convert(),
                    ClearValue = new Color
                    {
                        R = src.ClearValue.R,
                        G = src.ClearValue.G,
                        B = src.ClearValue.B,
                        A = src.ClearValue.A,
                    },
                };
            }

            RenderPassDepthStencilAttachment depthAttachment = default;
            RenderPassDepthStencilAttachment* depthPtr = null;

            if (descriptor.DepthStencilAttachment.HasValue)
            {
                var ds = descriptor.DepthStencilAttachment.Value;
                var wgpuView = (WgpuTextureView)ds.View;

                depthAttachment = new RenderPassDepthStencilAttachment
                {
                    View = wgpuView,
                    DepthLoadOp = ds.DepthLoadOp.Convert(),
                    DepthStoreOp = ds.DepthStoreOp.Convert(),
                    DepthClearValue = ds.DepthClearValue,
                    // Stencil defaults — no stencil ops unless we add them to the RHI
                    StencilLoadOp = LoadOp.Undefined,
                    StencilStoreOp = StoreOp.Undefined,
                    StencilReadOnly = true,
                };

                depthPtr = &depthAttachment;
            }

            var passDesc = new RenderPassDescriptor
            {
                ColorAttachmentCount = (uint)colorCount,
                ColorAttachments = colorAttachments,
                DepthStencilAttachment = depthPtr,
            };

            var encoder = _device.Wgpu.CommandEncoderBeginRenderPass(_handle, &passDesc);
            return new WgpuRenderPass(_device, encoder);
        }

        public override SlCommandBuffer Finish()
        {
            var handle = _device.Wgpu.CommandEncoderFinish(_handle, null);
            return new WgpuCommandBuffer(_device.Wgpu, handle);
        }

        public override void Dispose()
        {
            if (_handle != null)
                _device.Wgpu.CommandEncoderRelease(_handle);
        }
    }
}
