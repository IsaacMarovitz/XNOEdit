namespace Solaris.Builders
{
    public unsafe class BindGroupBuilder
    {
        private readonly SlDevice _device;
        private readonly List<SlBindGroupLayoutEntry> _layoutEntries = [];
        private readonly List<SlBindGroupEntry> _entries = [];
        private SlBindGroupLayout? _layout;

        public BindGroupBuilder(SlDevice device)
        {
            _device = device;
        }

        public BindGroupBuilder AddUniformBuffer<T>(
            uint binding,
            SlBuffer<T> buffer,
            SlShaderStage visibility = SlShaderStage.Vertex | SlShaderStage.Fragment) where T : unmanaged
        {
            _layoutEntries.Add(new SlBindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Type = SlBindingType.Buffer,
                BufferType = SlBufferBindingType.Uniform
            });

            _entries.Add(new SlBindGroupEntry
            {
                Binding = binding,
                Buffer = new SlBufferBinding
                {
                    Size = buffer.Size,
                    Offset = 0,
                    Handle = buffer.GetHandle()
                }
            });

            return this;
        }

        public BindGroupBuilder AddStorageBuffer<T>(
            uint binding,
            SlBuffer<T> buffer,
            SlShaderStage visibility = SlShaderStage.Vertex | SlShaderStage.Fragment,
            bool readOnly = false) where T : unmanaged
        {
            _layoutEntries.Add(new SlBindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Type = SlBindingType.Buffer,
                BufferType = readOnly ? SlBufferBindingType.ReadOnlyStorage : SlBufferBindingType.Storage,
            });

            _entries.Add(new SlBindGroupEntry
            {
                Binding = binding,
                Buffer = new SlBufferBinding
                {
                    Size = buffer.Size,
                    Offset = 0,
                    Handle = buffer.GetHandle()
                }
            });

            return this;
        }

        public BindGroupBuilder AddTexture(
            uint binding,
            SlTextureView textureView,
            SlShaderStage visibility = SlShaderStage.Fragment,
            SlTextureSampleType sampleType = SlTextureSampleType.Float,
            SlTextureViewDimension dimension = SlTextureViewDimension.Dimension2D)
        {
            _layoutEntries.Add(new SlBindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Type = SlBindingType.Texture,
                TextureSampleType = sampleType,
                TextureDimension = dimension
            });

            _entries.Add(new SlBindGroupEntry
            {
                Binding = binding,
                TextureView = textureView
            });

            return this;
        }

        public BindGroupBuilder AddSampler(
            uint binding,
            SlSampler sampler,
            SlShaderStage visibility = SlShaderStage.Fragment,
            SlSamplerBindingType type = SlSamplerBindingType.Filtering)
        {
            _layoutEntries.Add(new SlBindGroupLayoutEntry
            {
                Binding = binding,
                Visibility = visibility,
                Type = SlBindingType.Sampler,
                SamplerType = type,
            });

            _entries.Add(new SlBindGroupEntry
            {
                Binding = binding,
                Sampler = sampler
            });

            return this;
        }

        public SlBindGroupLayout BuildLayout()
        {
            if (_layout != null)
                return _layout;

            var entries = _layoutEntries.ToArray();

            var desc = new SlBindGroupLayoutDescriptor
            {
                Entries = entries
            };

            _layout = _device.CreateBindGroupLayout(desc);
            return _layout;
        }

        public SlBindGroup BuildBindGroup()
        {
            if (_layout == null)
                BuildLayout();

            var entries = _entries.ToArray();

            var desc = new SlBindGroupDescriptor
            {
                Layout = _layout,
                Entries = entries
            };

            return _device.CreateBindGroup(desc);
        }
    }
}
