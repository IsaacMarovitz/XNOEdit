using Silk.NET.WebGPU;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuQueue : SlQueue
    {
        private readonly WebGPU _wgpu;
        public Queue* Queue { get; }

        internal WgpuQueue(WebGPU wgpu, Queue* queue)
        {
            _wgpu = wgpu;
            Queue = queue;
        }

        public override void WriteTexture(SlCopyTextureDescriptor descriptor, Span<byte> data, SlTextureDataLayout layout, SlExtent3D extent)
        {
            fixed (byte* dataPtr = data)
                WriteTexture(descriptor, dataPtr, (nuint)data.Length, layout, extent);
        }

        public override void WriteTexture(SlCopyTextureDescriptor descriptor, byte* data, nuint size, SlTextureDataLayout layout, SlExtent3D extent)
        {
            var wgpuDescriptor = new ImageCopyTexture
            {
                Texture = (descriptor.Texture as WgpuTexture)!.Texture,
                MipLevel = descriptor.MipLevel,
                Origin = new Origin3D(descriptor.Origin.X, descriptor.Origin.Y, descriptor.Origin.Z)
            };

            var wgpuLayout = new TextureDataLayout
            {
                Offset = layout.Offset,
                BytesPerRow = layout.BytesPerRow,
                RowsPerImage = layout.RowsPerImage,
            };

            var wgpuExtent = new Extent3D(extent.Width, extent.Height, extent.DepthOrArrayLayers);

            _wgpu.QueueWriteTexture(Queue, &wgpuDescriptor, data, size, &wgpuLayout, &wgpuExtent);
        }

        public override void Submit(SlCommandBuffer commandBuffer)
        {
            var wgpuCommandBuffer = (commandBuffer as WgpuCommandBuffer)!.CommandBuffer;
            _wgpu.QueueSubmit(Queue, 1, &wgpuCommandBuffer);
        }

        public override void Dispose()
        {
            if (Queue != null)
                _wgpu.QueueRelease(Queue);
        }
    }
}
