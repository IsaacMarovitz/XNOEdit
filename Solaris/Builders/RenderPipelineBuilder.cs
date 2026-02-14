using Solaris.RHI;

namespace Solaris.Builders
{
    public class RenderPipelineBuilder(SlDevice device)
    {
        private SlShaderModule _shaderModule;
        private string _vertexEntry = "vs_main";
        private string _fragmentEntry = "fs_main";
        private SlPrimitiveTopology _topology = SlPrimitiveTopology.TriangleList;
        private SlCullMode _cullMode = SlCullMode.None;
        private SlFrontFace _frontFace = SlFrontFace.CounterClockwise;
        private readonly List<SlVertexBufferLayout> _vertexLayouts = [];
        private readonly List<SlBindGroupLayout> _bindGroupLayouts = [];

        private bool _hasDepth;
        private bool _depthWrite = true;
        private SlCompareFunction _depthCompare = SlCompareFunction.Greater;

        private bool _hasBlend;
        private SlBlendState _blendState;

        private const SlTextureFormat DepthTextureFormat = SlTextureFormat.Depth32float;

        public static implicit operator SlRenderPipeline(RenderPipelineBuilder b) => b.Build();

        public RenderPipelineBuilder WithShader(SlShaderModule module)
        {
            _shaderModule = module;
            return this;
        }

        public RenderPipelineBuilder WithEntryPoints(string vertex, string fragment)
        {
            _vertexEntry = vertex;
            _fragmentEntry = fragment;
            return this;
        }

        public RenderPipelineBuilder WithTopology(SlPrimitiveTopology topology)
        {
            _topology = topology;
            return this;
        }

        public RenderPipelineBuilder WithCulling(SlCullMode cullMode, SlFrontFace frontFace = SlFrontFace.CounterClockwise)
        {
            _cullMode = cullMode;
            _frontFace = frontFace;
            return this;
        }

        public RenderPipelineBuilder WithVertexLayout(SlVertexBufferLayout layout)
        {
            _vertexLayouts.Add(layout);
            return this;
        }

        public RenderPipelineBuilder WithVertexLayouts(SlVertexBufferLayout[] layouts)
        {
            _vertexLayouts.AddRange(layouts);
            return this;
        }

        public RenderPipelineBuilder WithBindGroupLayout(SlBindGroupLayout layout)
        {
            _bindGroupLayouts.Add(layout);
            return this;
        }

        public RenderPipelineBuilder WithBindGroupLayouts(SlBindGroupLayout[] layout)
        {
            _bindGroupLayouts.AddRange(layout);
            return this;
        }

        public RenderPipelineBuilder WithDepth(
            bool write = true,
            SlCompareFunction compare = SlCompareFunction.Greater)
        {
            _hasDepth = true;
            _depthWrite = write;
            _depthCompare = compare;
            return this;
        }

        public RenderPipelineBuilder WithAlphaBlend()
        {
            _hasBlend = true;
            _blendState = new SlBlendState
            {
                Color = new SlBlendComponent
                {
                    Operation = SlBlendOperation.Add,
                    SrcFactor = SlBlendFactor.SrcAlpha,
                    DstFactor = SlBlendFactor.OneMinusSrcAlpha
                },
                Alpha = new SlBlendComponent
                {
                    Operation = SlBlendOperation.Add,
                    SrcFactor = SlBlendFactor.One,
                    DstFactor = SlBlendFactor.OneMinusSrcAlpha
                }
            };
            return this;
        }

        public RenderPipelineBuilder WithCustomBlend(SlBlendState blendState)
        {
            _hasBlend = true;
            _blendState = blendState;
            return this;
        }

        public SlRenderPipeline Build()
        {
            if (_shaderModule == null)
                throw new InvalidOperationException("Shader module must be set");

            var variant = new SlPipelineVariantDescriptor
            {
                Topology = _topology,
                CullMode = _cullMode,
                FrontFace = _frontFace,
                DepthWrite = _depthWrite,
                DepthCompare = _depthCompare,
            };

            var descriptor = new SlRenderPipelineDescriptor
            {
                Shader = _shaderModule,
                VertexEntryPoint = _vertexEntry,
                FragmentEntryPoint = _fragmentEntry,
                VertexBufferLayouts = _vertexLayouts.ToArray(),
                BindGroupLayouts = _bindGroupLayouts.ToArray(),
                Variant = variant,
                ColorFormat = SlDevice.SurfaceFormat,
                DepthFormat = _hasDepth ? DepthTextureFormat : null,
                BlendState = _hasBlend ? _blendState : null,
            };

            return device.CreateRenderPipeline(descriptor);
        }
    }
}
