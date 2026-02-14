using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuBindGroup : SlBindGroup
    {
        private readonly WgpuDevice _device;
        internal BindGroup* Handle { get; }

        public WgpuBindGroup(WgpuDevice device, SlBindGroupDescriptor descriptor)
        {
            _device = device;

            var layout = (WgpuBindGroupLayout)descriptor.Layout;
            var entryCount = descriptor.Entries?.Length ?? 0;
            var entries = stackalloc BindGroupEntry[entryCount];

            for (var i = 0; i < entryCount; i++)
            {
                ref var src = ref descriptor.Entries![i];

                entries[i] = new BindGroupEntry
                {
                    Binding = src.Binding,
                };

                if (src.Buffer.HasValue)
                {
                    var buf = src.Buffer.Value;
                    entries[i].Buffer = (Buffer*)buf.Handle;
                    entries[i].Offset = buf.Offset;
                    entries[i].Size = buf.Size;
                }
                else if (src.TextureView != null)
                {
                    entries[i].TextureView = (TextureView*)src.TextureView.GetHandle();
                }
                else if (src.Sampler != null)
                {
                    entries[i].Sampler = (Sampler*)src.Sampler.GetHandle();
                }
            }

            var desc = new BindGroupDescriptor
            {
                Layout = layout.Handle,
                EntryCount = (uint)entryCount,
                Entries = entries,
            };

            Handle = _device.Wgpu.DeviceCreateBindGroup(_device, in desc);
        }

        public override void Dispose()
        {
            if (Handle != null)
                _device.Wgpu.BindGroupRelease(Handle);
        }
    }
}
