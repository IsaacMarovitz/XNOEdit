using Silk.NET.WebGPU;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuBindGroupLayout : SlBindGroupLayout
    {
        private readonly WgpuDevice _device;
        internal BindGroupLayout* Handle { get; }

        public WgpuBindGroupLayout(WgpuDevice device, SlBindGroupLayoutDescriptor descriptor)
        {
            _device = device;

            var entryCount = descriptor.Entries?.Length ?? 0;
            var entries = stackalloc BindGroupLayoutEntry[entryCount];

            for (var i = 0; i < entryCount; i++)
            {
                ref var src = ref descriptor.Entries![i];

                entries[i] = new BindGroupLayoutEntry
                {
                    Binding = src.Binding,
                    Visibility = src.Visibility.Convert(),
                };

                switch (src.Type)
                {
                    case SlBindingType.Buffer:
                        entries[i].Buffer = new BufferBindingLayout
                        {
                            Type = src.BufferType.Convert(),
                        };
                        break;

                    case SlBindingType.Texture:
                        entries[i].Texture = new TextureBindingLayout
                        {
                            SampleType = src.TextureSampleType.Convert(),
                            ViewDimension = src.TextureDimension.Convert(),
                        };
                        break;

                    case SlBindingType.Sampler:
                        entries[i].Sampler = new SamplerBindingLayout
                        {
                            Type = src.SamplerType.Convert(),
                        };
                        break;
                }
            }

            var desc = new BindGroupLayoutDescriptor
            {
                EntryCount = (uint)entryCount,
                Entries = entries,
            };

            Handle = _device.Wgpu.DeviceCreateBindGroupLayout(_device, in desc);
        }

        public override void Dispose()
        {
            if (Handle != null)
                _device.Wgpu.BindGroupLayoutRelease(Handle);
        }
    }
}
