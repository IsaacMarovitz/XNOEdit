using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuTexture : SlTexture
    {
        private readonly WgpuDevice _device;
        private readonly bool _owned;
        private readonly Texture* _handle;

        public static implicit operator Texture*(WgpuTexture texture) => texture._handle;

        internal WgpuTexture(WgpuDevice device, Texture* handle, bool owned = true)
        {
            _device = device;
            _owned = owned;
            _handle = handle;
        }

        internal WgpuTexture(WgpuDevice device, SlTextureDescriptor descriptor)
        {
            _device = device;
            _owned = true;

            var wgpuDescriptor = new TextureDescriptor
            {
                Size = new Extent3D
                {
                    Width = descriptor.Size.Width,
                    Height = descriptor.Size.Height,
                    DepthOrArrayLayers = descriptor.Size.DepthOrArrayLayers
                },
                MipLevelCount = descriptor.MipLevelCount,
                SampleCount = descriptor.SampleCount,
                Dimension = descriptor.Dimension.Convert(),
                Format = descriptor.Format.Convert(),
                Usage = descriptor.Usage.Convert(),
            };

            _handle = _device.Wgpu.DeviceCreateTexture(_device, &wgpuDescriptor);
        }

        public override SlTextureView CreateTextureView(SlTextureViewDescriptor descriptor)
        {
            return new WgpuTextureView(_device, this, descriptor);
        }

        public override void Dispose()
        {
            if (_handle != null && _owned)
            {
                _device.Wgpu.TextureDestroy(this);
                _device.Wgpu.TextureRelease(this);
            }
        }
    }
}
