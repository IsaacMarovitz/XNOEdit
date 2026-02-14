using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Solaris.RHI;
using Solaris.Wgpu;

namespace Solaris.Builders
{
    public unsafe class RenderPipelineBuilder(SlDevice device)
    {
        private ShaderModule* _shaderModule;
        private string _vertexEntry = "vs_main";
        private string _fragmentEntry = "fs_main";
        private SlPrimitiveTopology _topology = SlPrimitiveTopology.TriangleList;
        private SlCullMode _cullMode = SlCullMode.None;
        private SlFrontFace _frontFace = SlFrontFace.CounterClockwise;
        private readonly List<VertexBufferLayout> _vertexLayouts = [];
        private readonly List<IntPtr> _bindGroupLayouts = [];

        private bool _hasDepth;
        private bool _depthWrite = true;
        private SlCompareFunction _depthCompare = SlCompareFunction.Greater;

        private bool _hasBlend;
        private BlendState _blendState;

        private const TextureFormat DepthTextureFormat = TextureFormat.Depth32float;

        public static implicit operator RenderPipeline*(RenderPipelineBuilder b) => b.Build();

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

        public RenderPipelineBuilder WithBindGroupLayouts(IntPtr[] layout)
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
            // TODO: Cleanup
            var _wgpuDevice = device as WgpuDevice;
            var _wgpu = _wgpuDevice.Wgpu;

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
                    pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_wgpuDevice, &layoutDesc);
                }
            }
            else
            {
                pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_wgpuDevice, &layoutDesc);
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
                    Format = _wgpuDevice.GetSurfaceFormat(),
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
                        Format = DepthTextureFormat,
                        DepthWriteEnabled = _depthWrite,
                        DepthCompare = _depthCompare.Convert(),
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
                        Topology = _topology.Convert(),
                        FrontFace = _frontFace.Convert(),
                        CullMode = _cullMode.Convert(),
                    },
                    Multisample = new MultisampleState
                    {
                        Count = 1,
                        Mask = ~0u,
                        AlphaToCoverageEnabled = false
                    },
                    DepthStencil = _hasDepth ? &depthStencil : null
                };

                var pipeline = _wgpu.DeviceCreateRenderPipeline(_wgpuDevice, &pipelineDesc);
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
