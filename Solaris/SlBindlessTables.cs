using Plume;

namespace Solaris
{
    /// <summary>
    /// Two global bindless tables:
    /// - Texture table addressed by <see cref="SlTextureIndex"/>
    /// - Sampler table addressed by <see cref="SlSamplerIndex"/>.
    ///
    /// Both grow on demand. Indices are stable across growth.
    ///
    /// Registration is only legal outside a pass.
    /// </summary>
    public sealed unsafe class SlBindlessTables : IDisposable
    {
        private const uint InitialTextureCapacity = 1024;
        private const uint InitialSamplerCapacity = 64;

        private readonly RenderDevice* _device;
        private readonly SlGlobalLayout _layout;
        private readonly SlRetirementQueue _retirement;

        private RenderDescriptorSet* _textureSet;
        private RenderDescriptorSet* _samplerSet;

        private uint _textureCapacity;
        private uint _samplerCapacity;

        private readonly List<SlTexture?> _textures = [];
        private readonly Stack<uint> _freeTextureSlots = new();
        private uint _textureWatermark;

        private readonly List<nint> _samplers = [];
        private readonly Dictionary<SlSamplerDescriptor, SlSamplerIndex> _samplerCache = [];

        private readonly SlTexture[] _nullTextures = new SlTexture[SlTextureIndex.ReservedCount];

        private bool _disposed;

        internal SlBindlessTables(RenderDevice* device, SlGlobalLayout layout, SlRetirementQueue retirement)
        {
            _device = device;
            _layout = layout;
            _retirement = retirement;

            _textureCapacity = InitialTextureCapacity;
            _samplerCapacity = InitialSamplerCapacity;

            _textureSet = AllocateTextureSet(_textureCapacity);
            _samplerSet = AllocateSamplerSet(_samplerCapacity);

            CreateNullTextures();
            CreateDefaultSampler();
        }

        internal RenderDescriptorSet* TextureSet => _textureSet;

        internal RenderDescriptorSet* SamplerSet => _samplerSet;

        public uint TextureCapacity => _textureCapacity;

        public uint TextureCount => _textureWatermark;

        public SlTextureIndex Register(SlTexture texture, RenderTextureView* view = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(texture);

            var slot = AcquireTextureSlot();

            while (_textures.Count <= slot)
            {
                _textures.Add(null);
            }

            _textures[(int)slot] = texture;

            _textureSet->SetTexture(
                slot,
                texture.Handle,
                RenderTextureLayout.ShaderRead,
                view != null ? view : texture.DefaultView);

            return new SlTextureIndex(slot);
        }

        /// <summary>
        /// Releases a slot. The descriptor is pointed at the null texture immediately.
        /// The slot itself is only reusable once in-flight frames have completed.
        /// </summary>
        public void Release(SlTextureIndex index)
        {
            if (_disposed)
                return;

            var slot = index.Slot;

            if (slot < SlTextureIndex.ReservedCount)
                throw new ArgumentException("Cannot release a reserved null descriptor.", nameof(index));

            if (slot >= _textures.Count || _textures[(int)slot] == null)
                return;

            _textures[(int)slot] = null;

            var nullTexture = _nullTextures[0];
            _textureSet->SetTexture(slot, nullTexture.Handle, RenderTextureLayout.ShaderRead, nullTexture.DefaultView);

            _retirement.Retire(() => _freeTextureSlots.Push(slot));
        }

        private uint AcquireTextureSlot()
        {
            if (_freeTextureSlots.Count > 0)
                return _freeTextureSlots.Pop();

            if (_textureWatermark >= _textureCapacity)
            {
                GrowTextureTable(_textureWatermark + 1);
            }

            return _textureWatermark++;
        }

        private void GrowTextureTable(uint required)
        {
            var capacity = Math.Max(_textureCapacity * 2, required);

            if (capacity > SlGlobalLayout.TextureTableCeiling)
                throw new InvalidOperationException($"Texture table exceeded the layout ceiling of {SlGlobalLayout.TextureTableCeiling}.");

            var replacement = AllocateTextureSet(capacity);

            for (var slot = 0; slot < _textures.Count; slot++)
            {
                if (_textures[slot] is not { } texture)
                    continue;

                replacement->SetTexture((uint)slot, texture.Handle, RenderTextureLayout.ShaderRead, texture.DefaultView);
            }

            var superseded = _textureSet;
            _retirement.Retire(() => superseded->Dispose());

            _textureSet = replacement;
            _textureCapacity = capacity;
        }

        private RenderDescriptorSet* AllocateTextureSet(uint capacity)
        {
            var desc = _layout.TextureSetDesc;
            desc.BoundlessRangeSize = capacity;

            var set = _device->CreateDescriptorSet(&desc);

            if (set == null)
                throw new InvalidOperationException($"Failed to allocate a bindless texture set of {capacity} entries.");

            return set;
        }

        public SlSamplerIndex Register(in SlSamplerDescriptor descriptor)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_samplerCache.TryGetValue(descriptor, out var existing))
                return existing;

            var slot = (uint)_samplers.Count;

            if (slot >= _samplerCapacity)
            {
                GrowSamplerTable(slot + 1);
            }

            var plumeDesc = descriptor.ToPlume();
            var sampler = _device->CreateSampler(&plumeDesc);

            if (sampler == null)
                throw new InvalidOperationException("Failed to create a sampler.");

            _samplers.Add((nint)sampler);
            _samplerSet->SetSampler(slot, sampler);

            var index = new SlSamplerIndex(slot);
            _samplerCache[descriptor] = index;
            return index;
        }

        private void GrowSamplerTable(uint required)
        {
            var capacity = Math.Max(_samplerCapacity * 2, required);

            if (capacity > SlGlobalLayout.SamplerTableCeiling)
                throw new InvalidOperationException($"Sampler table exceeded the layout ceiling of {SlGlobalLayout.SamplerTableCeiling}.");

            var replacement = AllocateSamplerSet(capacity);

            for (var slot = 0; slot < _samplers.Count; slot++)
            {
                replacement->SetSampler((uint)slot, (RenderSampler*)_samplers[slot]);
            }

            var superseded = _samplerSet;
            _retirement.Retire(() => superseded->Dispose());

            _samplerSet = replacement;
            _samplerCapacity = capacity;
        }

        private RenderDescriptorSet* AllocateSamplerSet(uint capacity)
        {
            var desc = _layout.SamplerSetDesc;
            desc.BoundlessRangeSize = capacity;

            var set = _device->CreateDescriptorSet(&desc);

            if (set == null)
                throw new InvalidOperationException($"Failed to allocate a bindless sampler set of {capacity} entries.");

            return set;
        }

        /// <summary>
        /// Slots 0-2 are 1x1 textures with an all-zero component mapping, one per view
        /// dimension. Combined with PARTIALLY_BOUND this makes an unwritten or released
        /// slot sample black instead of undefined memory.
        /// </summary>
        private void CreateNullTextures()
        {
            for (var i = 0u; i < SlTextureIndex.ReservedCount; i++)
            {
                var isCube = i == SlTextureIndex.NullTextureCube.Slot;
                var isWhite = i == SlTextureIndex.WhiteTexture2D.Slot;

                var descriptor = new SlTextureDescriptor
                {
                    Width = 1,
                    Height = 1,
                    Depth = 1,
                    MipLevels = 1,
                    ArraySize = isCube ? 6u : 1u,
                    Format = RenderFormat.R8Unorm,
                    Dimension = RenderTextureDimension.Texture2D,
                    Usage = isCube ? SlTextureUsage.Sampled | SlTextureUsage.Cube : SlTextureUsage.Sampled,
                };

                var plumeDesc = new RenderTextureDesc
                {
                    Dimension = descriptor.Dimension,
                    Width = descriptor.Width,
                    Height = descriptor.Height,
                    Depth = descriptor.Depth,
                    MipLevels = descriptor.MipLevels,
                    ArraySize = descriptor.ArraySize,
                    Format = descriptor.Format,
                    Flags = descriptor.ToPlumeFlags(),
                };

                var texture = _device->CreateTexture(&plumeDesc);

                if (texture == null)
                    throw new InvalidOperationException("Failed to create a null descriptor texture.");

                var wrapper = new SlTexture(texture, descriptor, ownsTexture: true);
                _nullTextures[i] = wrapper;

                var viewDesc = new RenderTextureViewDesc
                {
                    Format = descriptor.Format,
                    Dimension = SlTexture.ToViewDimension(descriptor),
                    MipLevels = 1,
                    ArraySize = descriptor.ArraySize,
                    ComponentMapping = isWhite
                        ? new RenderComponentMapping(
                            RenderSwizzle.One, RenderSwizzle.One, RenderSwizzle.One, RenderSwizzle.One)
                        : new RenderComponentMapping(
                            RenderSwizzle.Zero, RenderSwizzle.Zero, RenderSwizzle.Zero, RenderSwizzle.Zero),
                };

                var view = texture->CreateTextureView(&viewDesc);

                _textures.Add(wrapper);
                _textureSet->SetTexture(i, texture, RenderTextureLayout.ShaderRead, view);
            }

            _textureWatermark = SlTextureIndex.ReservedCount;
        }

        private void CreateDefaultSampler()
        {
            var index = Register(SlSamplerDescriptor.LinearWrap);

            if (index != SlSamplerIndex.Default)
                throw new InvalidOperationException("The default sampler must occupy slot 0.");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var sampler in _samplers)
            {
                ((RenderSampler*)sampler)->Dispose();
            }

            _samplers.Clear();

            foreach (var nullTexture in _nullTextures)
            {
                nullTexture?.Dispose();
            }

            if (_textureSet != null)
            {
                _textureSet->Dispose();
                _textureSet = null;
            }

            if (_samplerSet != null)
            {
                _samplerSet->Dispose();
                _samplerSet = null;
            }
        }
    }
}
