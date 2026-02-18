using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuQueue : SlQueue
    {
        private readonly WebGPU _wgpu;
        private readonly Queue* _handle;

        public static implicit operator Queue*(WgpuQueue queue) => queue._handle;

        internal WgpuQueue(WebGPU wgpu, Queue* handle)
        {
            _wgpu = wgpu;
            _handle = handle;
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
                Texture = (descriptor.Texture as WgpuTexture)!,
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

            _wgpu.QueueWriteTexture(_handle, &wgpuDescriptor, data, size, &wgpuLayout, &wgpuExtent);
        }

        public override void Submit(SlCommandBuffer commandBuffer)
        {
            CommandBuffer* wgpuCommandBuffer = (commandBuffer as WgpuCommandBuffer)!;
            _wgpu.QueueSubmit(_handle, 1, &wgpuCommandBuffer);
        }

        public override void Dispose()
        {
            if (_handle != null)
                _wgpu.QueueRelease(_handle);
        }
    }
}
