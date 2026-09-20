using Plume;

namespace Solaris
{
    public sealed unsafe class SlMaterial : IDisposable
    {
        private readonly SlDevice _device;
        private readonly SlShader _vertexShader;
        private readonly SlShader? _pixelShader;
        private readonly SlVertexLayout _vertexLayout;
        private readonly Dictionary<(SlPipelineVariant Variant, SlPassSignature Signature), SlPipeline> _pipelines = [];

        private bool _disposed;

        public SlMaterial(
            SlDevice device,
            SlShader vertexShader,
            SlShader? pixelShader,
            SlVertexLayout vertexLayout,
            string? name = null)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(vertexShader);
            ArgumentNullException.ThrowIfNull(vertexLayout);

            _device = device;
            _vertexShader = vertexShader;
            _pixelShader = pixelShader;
            _vertexLayout = vertexLayout;

            Name = name ?? "Material";
        }

        public string Name { get; }

        public SlPipeline Pipeline(in SlPipelineVariant variant, in SlPassSignature signature)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var key = (variant, signature);

            if (_pipelines.TryGetValue(key, out var cached))
                return cached;

            var pipeline = Create(in variant, in signature);
            _pipelines[key] = pipeline;
            return pipeline;
        }

        private SlPipeline Create(in SlPipelineVariant state, in SlPassSignature signature)
        {
            var slots = _vertexLayout.Slots;
            var elements = _vertexLayout.Elements;

            fixed (RenderInputSlot* slotPointer = slots)
            fixed (RenderInputElement* elementPointer = elements)
            {
                var specConstant = new RenderSpecConstant { Index = 0, Value = state.SpecConstants };

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

                    SpecConstants = state.SpecConstants != 0 ? &specConstant : null,
                    SpecConstantsCount = state.SpecConstants != 0 ? 1u : 0u,
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
