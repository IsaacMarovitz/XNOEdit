using Silk.NET.WebGPU;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuSurface : SlSurface
    {
        private readonly WebGPU _api;
        private Device* _device;
        internal Surface* Handle { get; }

        public WgpuSurface(WebGPU api, Surface* handle, Device* device)
        {
            _api = api;
            _device = device;
            Handle = handle;
        }

        public override SlTexture? GetCurrentTexture()
        {
            SurfaceTexture surfaceTexture;
            _api.SurfaceGetCurrentTexture(Handle, &surfaceTexture);

            if (surfaceTexture.Status != SurfaceGetCurrentTextureStatus.Success)
                return null;

            return new WgpuTexture(_api, surfaceTexture.Texture, owned: false);
        }

        public override void Present()
        {
            _api.SurfacePresent(Handle);
        }

        public void SetDevice(Device* device)
        {
            _device = device;
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

            _api.SurfaceConfigure(Handle, &config);
        }

        public override void Dispose()
        {
            if (Handle != null)
            {
                _api.SurfaceUnconfigure(Handle);
                _api.SurfaceRelease(Handle);
            }
        }
    }
}
