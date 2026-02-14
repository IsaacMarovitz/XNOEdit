using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuTexture : SlTexture
    {
        private readonly WebGPU _wgpu;
        private readonly bool _owned;
        public Texture* Texture { get; }

        internal WgpuTexture(WebGPU wgpu, Texture* texture, bool owned = true)
        {
            _wgpu = wgpu;
            Texture = texture;
            _owned = owned;
        }

        public override void* GetHandle()
        {
            return Texture;
        }

        public override SlTextureView CreateTextureView(SlTextureViewDescriptor descriptor)
        {
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

            return new WgpuTextureView(_wgpu, _wgpu.TextureCreateView(Texture, &wgpuDescriptor));
        }

        public override void Dispose()
        {
            if (Texture != null && _owned)
            {
                _wgpu.TextureDestroy(Texture);
                _wgpu.TextureRelease(Texture);
            }
        }
    }
}
