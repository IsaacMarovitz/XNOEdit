using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuTextureView : SlTextureView
    {
        private readonly WgpuDevice _device;
        private readonly TextureView* _handle;

        public static implicit operator TextureView*(WgpuTextureView textureView) => textureView._handle;

        internal WgpuTextureView(WgpuDevice device, WgpuTexture texture, SlTextureViewDescriptor descriptor)
        {
            _device = device;

            var wgpuDescriptor = new TextureViewDescriptor
            {
                Format = descriptor.Format.Convert(),
                Dimension = descriptor.Dimension.Convert(),
                BaseMipLevel = descriptor.BaseMipLevel,
                MipLevelCount = descriptor.MipLevelCount,
                BaseArrayLayer = descriptor.BaseArrayLayer,
                ArrayLayerCount = descriptor.ArrayLayerCount,
                Aspect = TextureAspect.All
            };

            _handle = _device.Wgpu.TextureCreateView(texture, &wgpuDescriptor);
        }

        public override void* GetHandle()
        {
            return _handle;
        }

        public override void Dispose()
        {
            if (_handle != null)
                _device.Wgpu.TextureViewRelease(_handle);
        }
    }
}
