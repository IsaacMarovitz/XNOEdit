using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuRenderPipeline : SlRenderPipeline
    {
        private readonly WgpuDevice _device;
        private readonly RenderPipeline* _handle;

        public static implicit operator RenderPipeline*(WgpuRenderPipeline pipeline) => pipeline._handle;

        public WgpuRenderPipeline(WgpuDevice device, SlRenderPipelineDescriptor descriptor)
        {
            _device = device;

            var shaderModule = (WgpuShaderModule)descriptor.Shader;

            var vertexEntry = SilkMarshal.StringToPtr(descriptor.VertexEntryPoint ?? "vs_main");
            var fragmentEntry = SilkMarshal.StringToPtr(descriptor.FragmentEntryPoint ?? "fs_main");

            try
            {
                var layoutCount = descriptor.BindGroupLayouts?.Length ?? 0;
                var bgLayouts = stackalloc BindGroupLayout*[layoutCount];

                for (var i = 0; i < layoutCount; i++)
                    bgLayouts[i] = (WgpuBindGroupLayout)descriptor.BindGroupLayouts![i];

                var pipelineLayoutDesc = new PipelineLayoutDescriptor
                {
                    BindGroupLayoutCount = (uint)layoutCount,
                    BindGroupLayouts = bgLayouts,
                };

                var pipelineLayout = _device.Wgpu.DeviceCreatePipelineLayout(_device, &pipelineLayoutDesc);

                var vbCount = descriptor.VertexBufferLayouts?.Length ?? 0;
                var vbLayouts = stackalloc VertexBufferLayout[vbCount];

                var totalAttribCount = 0;
                for (var i = 0; i < vbCount; i++)
                    totalAttribCount += descriptor.VertexBufferLayouts![i].Attributes?.Length ?? 0;

                var allAttribs = stackalloc VertexAttribute[totalAttribCount];
                var attribOffset = 0;

                for (var i = 0; i < vbCount; i++)
                {
                    ref var src = ref descriptor.VertexBufferLayouts![i];
                    var attribCount = src.Attributes?.Length ?? 0;

                    for (var j = 0; j < attribCount; j++)
                    {
                        ref var attr = ref src.Attributes![j];
                        allAttribs[attribOffset + j] = new VertexAttribute
                        {
                            Format = attr.Format.Convert(),
                            Offset = attr.Offset,
                            ShaderLocation = attr.ShaderLocation,
                        };
                    }

                    vbLayouts[i] = new VertexBufferLayout
                    {
                        ArrayStride = src.Stride,
                        StepMode = src.StepMode == SlVertexStepMode.Instance
                            ? VertexStepMode.Instance
                            : VertexStepMode.Vertex,
                        AttributeCount = (uint)attribCount,
                        Attributes = &allAttribs[attribOffset],
                    };

                    attribOffset += attribCount;
                }

                var vertexState = new VertexState
                {
                    Module = shaderModule,
                    EntryPoint = (byte*)vertexEntry,
                    BufferCount = (uint)vbCount,
                    Buffers = vbLayouts,
                };

                BlendState blendState;
                var colorTarget = new ColorTargetState
                {
                    Format = descriptor.ColorFormat.Convert(),
                    WriteMask = ColorWriteMask.All,
                };

                if (descriptor.BlendState.HasValue)
                {
                    var bs = descriptor.BlendState.Value;
                    blendState = new BlendState
                    {
                        Color = new BlendComponent
                        {
                            Operation = bs.Color.Operation.Convert(),
                            SrcFactor = bs.Color.SrcFactor.Convert(),
                            DstFactor = bs.Color.DstFactor.Convert(),
                        },
                        Alpha = new BlendComponent
                        {
                            Operation = bs.Alpha.Operation.Convert(),
                            SrcFactor = bs.Alpha.SrcFactor.Convert(),
                            DstFactor = bs.Alpha.DstFactor.Convert(),
                        },
                    };
                    colorTarget.Blend = &blendState;
                }
                else if (descriptor.Variant?.AlphaBlend == true)
                {
                    blendState = new BlendState
                    {
                        Color = new BlendComponent
                        {
                            Operation = BlendOperation.Add,
                            SrcFactor = BlendFactor.SrcAlpha,
                            DstFactor = BlendFactor.OneMinusSrcAlpha,
                        },
                        Alpha = new BlendComponent
                        {
                            Operation = BlendOperation.Add,
                            SrcFactor = BlendFactor.One,
                            DstFactor = BlendFactor.OneMinusSrcAlpha,
                        },
                    };
                    colorTarget.Blend = &blendState;
                }

                var fragmentState = new FragmentState
                {
                    Module = shaderModule,
                    EntryPoint = (byte*)fragmentEntry,
                    TargetCount = 1,
                    Targets = &colorTarget,
                };

                DepthStencilState depthStencil;
                DepthStencilState* depthPtr = null;

                if (descriptor.DepthFormat.HasValue)
                {
                    depthStencil = new DepthStencilState
                    {
                        Format = descriptor.DepthFormat.Value.Convert(),
                        DepthWriteEnabled = descriptor.Variant?.DepthWrite ?? true,
                        DepthCompare = (descriptor.Variant?.DepthCompare ?? SlCompareFunction.Less).Convert(),
                        StencilFront = DefaultStencilFace(),
                        StencilBack = DefaultStencilFace(),
                        StencilReadMask = 0xFFFFFFFF,
                        StencilWriteMask = 0xFFFFFFFF,
                    };
                    depthPtr = &depthStencil;
                }

                var variant = descriptor.Variant ?? new SlPipelineVariantDescriptor();

                var primitiveState = new PrimitiveState
                {
                    Topology = variant.Topology.Convert(),
                    FrontFace = variant.FrontFace.Convert(),
                    CullMode = variant.CullMode.Convert()
                };

                var pipelineDesc = new RenderPipelineDescriptor
                {
                    Layout = pipelineLayout,
                    Vertex = vertexState,
                    Fragment = &fragmentState,
                    Primitive = primitiveState,
                    Multisample = new MultisampleState
                    {
                        Count = 1,
                        Mask = ~0u,
                        AlphaToCoverageEnabled = false,
                    },
                    DepthStencil = depthPtr,
                };

                _handle = _device.Wgpu.DeviceCreateRenderPipeline(_device, in pipelineDesc);

                _device.Wgpu.PipelineLayoutRelease(pipelineLayout);
            }
            finally
            {
                SilkMarshal.Free(vertexEntry);
                SilkMarshal.Free(fragmentEntry);
            }
        }

        private static StencilFaceState DefaultStencilFace() => new()
        {
            Compare = CompareFunction.Always,
            FailOp = StencilOperation.Keep,
            DepthFailOp = StencilOperation.Keep,
            PassOp = StencilOperation.Keep,
        };

        public override void Dispose()
        {
            if (_handle != null)
                _device.Wgpu.RenderPipelineRelease(_handle);
        }
    }
}
