using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalSampler : SlSampler
    {
        private MTLSamplerState _handle;

        public static implicit operator MTLSamplerState(MetalSampler sampler) => sampler._handle;

        internal MetalSampler(MetalDevice device, SlSamplerDescriptor descriptor)
        {
            var metalDescriptor = new MTLSamplerDescriptor
            {
                SAddressMode = descriptor.AddressModeU.Convert(),
                TAddressMode = descriptor.AddressModeV.Convert(),
                RAddressMode = descriptor.AddressModeW.Convert(),
                MagFilter = descriptor.MagFilter.Convert(),
                MinFilter = descriptor.MinFilter.Convert(),
                MipFilter = descriptor.MipmapFilter.MipmapConvert(),
                LodMaxClamp = descriptor.LodMaxClamp,
                LodMinClamp = descriptor.LodMinClamp,
                MaxAnisotropy = descriptor.MaxAnisotropy,
            };

            _handle = device.Device.NewSamplerState(metalDescriptor);
            metalDescriptor.Dispose();
        }

        public override void Dispose()
        {
            if (_handle.NativePtr != IntPtr.Zero)
            {
                _handle.Dispose();
            }
        }
    }
}
