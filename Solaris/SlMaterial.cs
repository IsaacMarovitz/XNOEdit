using Plume;

namespace Solaris
{
    /// <summary>
    /// A shader pair, a vertex layout, and a set of named pipeline variants.
    /// </summary>
    public sealed unsafe class SlMaterial : IDisposable
    {
        private readonly SlDevice _device;
        private readonly SlShader _vertexShader;
        private readonly SlShader? _pixelShader;
        private readonly SlVertexLayout _vertexLayout;
        private readonly Dictionary<string, SlPipelineVariant> _variants;
        private readonly Dictionary<(string Variant, SlPassSignature Signature), SlPipeline> _pipelines = [];

        private bool _disposed;

        public SlMaterial(
            SlDevice device,
            SlShader vertexShader,
            SlShader? pixelShader,
            SlVertexLayout vertexLayout,
            IReadOnlyDictionary<string, SlPipelineVariant> variants,
            string? name = null)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(vertexShader);
            ArgumentNullException.ThrowIfNull(vertexLayout);
            ArgumentNullException.ThrowIfNull(variants);

            if (variants.Count == 0)
                throw new ArgumentException("A material needs at least one pipeline variant.", nameof(variants));

            _device = device;
            _vertexShader = vertexShader;
            _pixelShader = pixelShader;
            _vertexLayout = vertexLayout;
            _variants = new Dictionary<string, SlPipelineVariant>(variants);

            Name = name ?? "Material";
            DefaultVariant = variants.Keys.First();
        }

        public string Name { get; }

        /// <summary>The first declared variant, used when a draw names none.</summary>
        public string DefaultVariant { get; }

        public IReadOnlyCollection<string> Variants => _variants.Keys;

        /// <summary>
        /// Returns the pipeline for a variant in the given pass, creating it on first use.
        /// </summary>
        public SlPipeline Pipeline(string variant, in SlPassSignature signature)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var key = (variant, signature);

            if (_pipelines.TryGetValue(key, out var cached))
                return cached;

            if (!_variants.TryGetValue(variant, out var state))
            {
                throw new ArgumentException(
                    $"'{Name}' has no variant named '{variant}'. Known variants: {string.Join(", ", _variants.Keys)}.",
                    nameof(variant));
            }

            var pipeline = Create(state, signature);
            _pipelines[key] = pipeline;
            return pipeline;
        }

        public SlPipeline Pipeline(in SlPassSignature signature) => Pipeline(DefaultVariant, signature);

        /// <summary>
        /// Creates every variant for a signature up front. Worth calling once per pass
        /// configuration after load to keep pipeline compilation out of the first frame
        /// that happens to need it.
        /// </summary>
        public void Warm(in SlPassSignature signature)
        {
            foreach (var variant in _variants.Keys)
            {
                Pipeline(variant, signature);
            }
        }

        private SlPipeline Create(in SlPipelineVariant state, in SlPassSignature signature)
        {
            var slots = _vertexLayout.Slots;
            var elements = _vertexLayout.Elements;

            fixed (RenderInputSlot* slotPointer = slots)
            fixed (RenderInputElement* elementPointer = elements)
            {
                var desc = new RenderGraphicsPipelineDesc
                {
                    PipelineLayout = _device.Layout.Handle,
                    VertexShader = _vertexShader.Handle,
                    PixelShader = _pixelShader != null ? _pixelShader.Handle : null,

                    PrimitiveTopology = state.Topology,
                    CullMode = state.CullMode,
                    FrontFace = state.FrontFace,

                    DepthEnabled = state.DepthTest ?? signature.DepthFormat != RenderFormat.Unknown,
                    DepthWriteEnabled = state.DepthWrite,
                    DepthFunction = state.DepthCompare,
                    DepthTargetFormat = signature.DepthFormat,

                    RenderTargetCount = signature.ColorCount,
                    Multisampling = new RenderMultisampling((RenderSampleCounts)signature.SampleCount),

                    InputSlots = slotPointer,
                    InputSlotsCount = (uint)slots.Length,
                    InputElements = elementPointer,
                    InputElementsCount = (uint)elements.Length,
                };

                if (signature.ColorCount > 0)
                {
                    desc.RenderTargetFormat[0] = signature.ColorFormat0;
                    desc.RenderTargetBlend[0] = state.ToBlendDesc();
                }

                if (state.DepthBias is { } bias)
                {
                    desc.DepthBias = (int)bias.Constant;
                    desc.DepthBiasClamp = bias.Clamp;
                    desc.SlopeScaledDepthBias = bias.SlopeScaled;
                }

                var handle = _device.Handle->CreateGraphicsPipeline(&desc);

                if (handle == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to create a pipeline for '{Name}'. Check that the shader's vertex inputs match the layout " +
                        $"and that {signature.ColorFormat0} is a valid render target format.");
                }

                return new SlPipeline(handle, signature);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var pipeline in _pipelines.Values)
            {
                _device.Retire(pipeline);
            }

            _pipelines.Clear();

            _device.Retire(_vertexLayout);
            _device.Retire(_vertexShader);

            if (_pixelShader != null)
            {
                _device.Retire(_pixelShader);
            }
        }
    }
}
