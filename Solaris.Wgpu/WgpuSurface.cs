using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuSurface : SlSurface
    {
        private readonly WgpuDevice _device;
        private readonly Surface* _handle;

        public static implicit operator Surface*(WgpuSurface surface) => surface._handle;

        public WgpuSurface(WgpuDevice device, Surface* handle)
        {
            _device = device;
            _handle = handle;
        }

        public override SlTexture? GetCurrentTexture()
        {
            SurfaceTexture surfaceTexture;
            _device.Wgpu.SurfaceGetCurrentTexture(_handle, &surfaceTexture);

            if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
                return null;

            return new WgpuTexture(_device, surfaceTexture.Texture, owned: false);
        }

        public override void Present()
        {
            _device.Wgpu.SurfacePresent(_handle);
        }

        public override void Configure(SlSurfaceDescriptor descriptor)
        {
            var config = new SurfaceConfiguration
            {
                Device = _device,
                Format = descriptor.Format.Convert(),
                Usage = descriptor.Usage.Convert(),
                Width = descriptor.Width,
                Height = descriptor.Height,
                PresentMode = descriptor.PresentMode.Convert(),
                AlphaMode = CompositeAlphaMode.Auto,
            };

            _device.Wgpu.SurfaceConfigure(_handle, &config);
        }

        public override void Dispose()
        {
            if (_handle != null)
            {
                _device.Wgpu.SurfaceUnconfigure(_handle);
                _device.Wgpu.SurfaceRelease(_handle);
            }
        }
    }
}
