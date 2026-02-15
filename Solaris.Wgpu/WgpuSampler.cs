using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuSampler : SlSampler
    {
        private readonly WgpuDevice _device;
        private readonly Sampler* _handle;

        public static implicit operator Sampler*(WgpuSampler sampler) => sampler._handle;

        internal WgpuSampler(WgpuDevice device, SlSamplerDescriptor descriptor)
        {
            _device = device;

            var wgpuDescriptor = new SamplerDescriptor
            {
                AddressModeU = descriptor.AddressModeU.Convert(),
                AddressModeV = descriptor.AddressModeV.Convert(),
                AddressModeW = descriptor.AddressModeW.Convert(),
                MagFilter = descriptor.MagFilter.Convert(),
                MinFilter = descriptor.MinFilter.Convert(),
                MipmapFilter = descriptor.MipmapFilter.MipmapConvert(),
                LodMaxClamp = descriptor.LodMaxClamp,
                LodMinClamp = descriptor.LodMinClamp,
                MaxAnisotropy = descriptor.MaxAnisotropy,
            };

            _handle = _device.Wgpu.DeviceCreateSampler(_device, &wgpuDescriptor);
        }

        public override void Dispose()
        {
            if (_handle != null)
            {
                _device.Wgpu.SamplerRelease(_handle);
            }
        }
    }
}
