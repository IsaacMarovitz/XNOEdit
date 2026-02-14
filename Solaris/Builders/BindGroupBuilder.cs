using Silk.NET.WebGPU;
using Solaris.RHI;
using Solaris.Wgpu;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Solaris.Builders
{
    public unsafe class BindGroupBuilder
    {
        private readonly SlDevice _device;
        private readonly List<BindGroupLayoutEntry> _layoutEntries = [];
        private readonly List<BindGroupEntry> _entries = [];
        private BindGroupLayout* _layout;

        public BindGroupBuilder(SlDevice device)
        {
            _device = device;
        }

        public BindGroupBuilder AddUniformBuffer(
            uint binding,
            Buffer* buffer,
            ulong size,
            ShaderStage visibility = ShaderStage.Vertex | ShaderStage.Fragment)
        {
            _layoutEntries.Add(new BindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Buffer = new BufferBindingLayout
                {
                    Type = BufferBindingType.Uniform,
                    MinBindingSize = 0
                }
            });

            _entries.Add(new BindGroupEntry
            {
                Binding = binding,
                Buffer = buffer,
                Offset = 0,
                Size = size
            });

            return this;
        }

        public BindGroupBuilder AddUniformBuffer<T>(
            uint binding,
            SlBuffer<T> buffer,
            ShaderStage visibility = ShaderStage.Vertex | ShaderStage.Fragment) where T : unmanaged
        {
            return AddUniformBuffer(binding, (Buffer*)buffer.GetHandle(), buffer.Size, visibility);
        }

        public BindGroupBuilder AddStorageBuffer(
            uint binding,
            Buffer* buffer,
            ulong size,
            ShaderStage visibility = ShaderStage.Vertex | ShaderStage.Fragment,
            bool readOnly = false)
        {
            _layoutEntries.Add(new BindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Buffer = new BufferBindingLayout
                {
                    Type = readOnly ? BufferBindingType.ReadOnlyStorage : BufferBindingType.Storage,
                    MinBindingSize = 0
                }
            });

            _entries.Add(new BindGroupEntry
            {
                Binding = binding,
                Buffer = buffer,
                Offset = 0,
                Size = size
            });

            return this;
        }

        public BindGroupBuilder AddTexture(
            uint binding,
            TextureView* textureView,
            ShaderStage visibility = ShaderStage.Fragment,
            TextureSampleType sampleType = TextureSampleType.Float,
            TextureViewDimension dimension = TextureViewDimension.Dimension2D)
        {
            _layoutEntries.Add(new BindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Texture = new TextureBindingLayout
                {
                    SampleType = sampleType,
                    ViewDimension = dimension
                }
            });

            _entries.Add(new BindGroupEntry
            {
                Binding = binding,
                TextureView = textureView
            });

            return this;
        }

        public BindGroupBuilder AddSampler(
            uint binding,
            Sampler* sampler,
            ShaderStage visibility = ShaderStage.Fragment,
            SamplerBindingType type = SamplerBindingType.Filtering)
        {
            _layoutEntries.Add(new BindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Sampler = new SamplerBindingLayout
                {
                    Type = type
                }
            });

            _entries.Add(new BindGroupEntry
            {
                Binding = binding,
                Sampler = sampler
            });

            return this;
        }

        public BindGroupLayout* BuildLayout()
        {
            if (_layout != null)
                return _layout;

            var entries = _layoutEntries.ToArray();

            // TODO: Clean this up
            var wgpuDevice = _device as WgpuDevice;

            fixed (BindGroupLayoutEntry* pEntries = entries)
            {
                var desc = new BindGroupLayoutDescriptor
                {
                    EntryCount = (uint)entries.Length,
                    Entries = pEntries
                };

                _layout = wgpuDevice.Wgpu.DeviceCreateBindGroupLayout(wgpuDevice, &desc);
                return _layout;
            }
        }

        public BindGroup* BuildBindGroup()
        {
            if (_layout == null)
                BuildLayout();

            var entries = _entries.ToArray();

            // TODO: Clean this up
            var wgpuDevice = _device as WgpuDevice;

            fixed (BindGroupEntry* pEntries = entries)
            {
                var desc = new BindGroupDescriptor
                {
                    Layout = _layout,
                    EntryCount = (uint)entries.Length,
                    Entries = pEntries
                };

                return wgpuDevice.Wgpu.DeviceCreateBindGroup(wgpuDevice, &desc);
            }
        }
    }
}
