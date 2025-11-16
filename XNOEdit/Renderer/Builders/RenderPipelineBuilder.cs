using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace XNOEdit.Renderer.Builders
{
    public unsafe class RenderPipelineBuilder
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;

        private ShaderModule* _shaderModule;
        private string _vertexEntry = "vs_main";
        private string _fragmentEntry = "fs_main";
        private readonly TextureFormat _colorFormat;
        private PrimitiveTopology _topology = PrimitiveTopology.TriangleList;
        private CullMode _cullMode = CullMode.None;
        private FrontFace _frontFace = FrontFace.Ccw;
        private readonly List<VertexBufferLayout> _vertexLayouts = [];
        private readonly List<IntPtr> _bindGroupLayouts = [];

        private bool _hasDepth;
        private TextureFormat _depthFormat = TextureFormat.Depth24Plus;
        private bool _depthWrite = true;
        private CompareFunction _depthCompare = CompareFunction.Less;

        private bool _hasBlend;
        private BlendState _blendState;

        public RenderPipelineBuilder(WebGPU wgpu, Device* device, TextureFormat colorFormat)
        {
            _wgpu = wgpu;
            _device = device;
            _colorFormat = colorFormat;
        }

        public RenderPipelineBuilder WithShader(ShaderModule* module)
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

        public RenderPipelineBuilder WithTopology(PrimitiveTopology topology)
        {
            _topology = topology;
            return this;
        }

        public RenderPipelineBuilder WithCulling(CullMode cullMode, FrontFace frontFace = FrontFace.Ccw)
        {
            _cullMode = cullMode;
            _frontFace = frontFace;
            return this;
        }

        public RenderPipelineBuilder WithVertexLayout(VertexBufferLayout layout)
        {
            _vertexLayouts.Add(layout);
            return this;
        }

        public RenderPipelineBuilder WithVertexLayouts(params VertexBufferLayout[] layouts)
        {
            _vertexLayouts.AddRange(layouts);
            return this;
        }

        public RenderPipelineBuilder WithBindGroupLayout(BindGroupLayout* layout)
        {
            _bindGroupLayouts.Add((IntPtr)layout);
            return this;
        }

        public RenderPipelineBuilder WithDepth(
            TextureFormat format = TextureFormat.Depth24Plus,
            bool write = true,
            CompareFunction compare = CompareFunction.Less)
        {
            _hasDepth = true;
            _depthFormat = format;
            _depthWrite = write;
            _depthCompare = compare;
            return this;
        }

        public RenderPipelineBuilder WithAlphaBlend()
        {
            _hasBlend = true;
            _blendState = new BlendState
            {
                Color = new BlendComponent
                {
                    Operation = BlendOperation.Add,
                    SrcFactor = BlendFactor.SrcAlpha,
                    DstFactor = BlendFactor.OneMinusSrcAlpha
                },
                Alpha = new BlendComponent
                {
                    Operation = BlendOperation.Add,
                    SrcFactor = BlendFactor.One,
                    DstFactor = BlendFactor.OneMinusSrcAlpha
                }
            };
            return this;
        }

        public RenderPipelineBuilder WithCustomBlend(BlendState blendState)
        {
            _hasBlend = true;
            _blendState = blendState;
            return this;
        }

        public RenderPipeline* Build()
        {
            if (_shaderModule == null)
                throw new InvalidOperationException("Shader module must be set");

            // Create pipeline layout
            var layouts = _bindGroupLayouts.ToArray();
            var layoutDesc = new PipelineLayoutDescriptor();

            PipelineLayout* pipelineLayout;
            if (layouts.Length > 0)
            {
                fixed (IntPtr* pLayouts = layouts)
                {
                    layoutDesc.BindGroupLayoutCount = (uint)layouts.Length;
                    layoutDesc.BindGroupLayouts = (BindGroupLayout**)pLayouts;
                    pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, &layoutDesc);
                }
            }
            else
            {
                pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, &layoutDesc);
            }

            var vertexEntry = SilkMarshal.StringToPtr(_vertexEntry);
            var fragmentEntry = SilkMarshal.StringToPtr(_fragmentEntry);

            try
            {
                // Vertex state
                var vertexLayouts = _vertexLayouts.ToArray();
                VertexState vertexState;

                if (vertexLayouts.Length > 0)
                {
                    fixed (VertexBufferLayout* pLayouts = vertexLayouts)
                    {
                        vertexState = new VertexState
                        {
                            Module = _shaderModule,
                            EntryPoint = (byte*)vertexEntry,
                            BufferCount = (uint)vertexLayouts.Length,
                            Buffers = pLayouts
                        };
                    }
                }
                else
                {
                    vertexState = new VertexState
                    {
                        Module = _shaderModule,
                        EntryPoint = (byte*)vertexEntry,
                        BufferCount = 0,
                        Buffers = null
                    };
                }

                var colorTarget = new ColorTargetState
                {
                    Format = _colorFormat,
                    WriteMask = ColorWriteMask.All,
                    Blend = null
                };

                if (_hasBlend)
                {
                    var blendState = _blendState;
                    colorTarget.Blend = &blendState;
                }

                var fragmentState = new FragmentState
                {
                    Module = _shaderModule,
                    EntryPoint = (byte*)fragmentEntry,
                    TargetCount = 1,
                    Targets = &colorTarget
                };

                DepthStencilState depthStencil = default;
                if (_hasDepth)
                {
                    depthStencil = new DepthStencilState
                    {
                        Format = _depthFormat,
                        DepthWriteEnabled = _depthWrite,
                        DepthCompare = _depthCompare,
                        StencilFront = new StencilFaceState
                        {
                            Compare = CompareFunction.Always,
                            FailOp = StencilOperation.Keep,
                            DepthFailOp = StencilOperation.Keep,
                            PassOp = StencilOperation.Keep
                        },
                        StencilBack = new StencilFaceState
                        {
                            Compare = CompareFunction.Always,
                            FailOp = StencilOperation.Keep,
                            DepthFailOp = StencilOperation.Keep,
                            PassOp = StencilOperation.Keep
                        },
                        StencilReadMask = 0xFFFFFFFF,
                        StencilWriteMask = 0xFFFFFFFF
                    };
                }

                var pipelineDesc = new RenderPipelineDescriptor
                {
                    Layout = pipelineLayout,
                    Vertex = vertexState,
                    Fragment = &fragmentState,
                    Primitive = new PrimitiveState
                    {
                        Topology = _topology,
                        FrontFace = _frontFace,
                        CullMode = _cullMode
                    },
                    Multisample = new MultisampleState
                    {
                        Count = 1,
                        Mask = ~0u,
                        AlphaToCoverageEnabled = false
                    },
                    DepthStencil = _hasDepth ? &depthStencil : null
                };

                var pipeline = _wgpu.DeviceCreateRenderPipeline(_device, &pipelineDesc);
                _wgpu.PipelineLayoutRelease(pipelineLayout);

                return pipeline;
            }
            finally
            {
                SilkMarshal.Free(vertexEntry);
                SilkMarshal.Free(fragmentEntry);
            }
        }
    }
}
