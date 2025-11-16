using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer
{
    // Uniform structure matching WGSL shader layout
    [StructLayout(LayoutKind.Explicit, Size = 240)]
    public struct BasicModelUniforms
    {
        [FieldOffset(0)]
        public Matrix4x4 Model;

        [FieldOffset(64)]
        public Matrix4x4 View;

        [FieldOffset(128)]
        public Matrix4x4 Projection;

        [FieldOffset(192)]
        public Vector3 LightDir;

        // Padding at 204 (implicit)

        [FieldOffset(208)]
        public Vector3 LightColor;

        // Padding at 220 (implicit)

        [FieldOffset(224)]
        public Vector3 ViewPos;

        [FieldOffset(236)]
        public float VertColorStrength;
    }

    public unsafe class WgpuShader : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;
        private readonly Queue* _queue;
        private readonly ShaderModule* _vertexModule;
        private readonly ShaderModule* _fragmentModule;
        private readonly RenderPipeline* _pipeline;
        private readonly RenderPipeline* _pipelineCull;
        private readonly RenderPipeline* _wireframePipeline;
        private BindGroupLayout* _uniformBindGroupLayout;
        private Buffer* _uniformBuffer;
        private BindGroup* _uniformBindGroup;

        public BindGroup* UniformBindGroup => _uniformBindGroup;

        public WgpuShader(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            string shaderSource,
            TextureFormat swapChainFormat,
            VertexBufferLayout[] vertexLayouts)
        {
            _wgpu = wgpu;
            _device = device;
            _queue = queue;

            _vertexModule = CreateShaderModule(device, shaderSource);
            _fragmentModule = _vertexModule;

            CreateUniformResources(device);

            _pipeline = CreateRenderPipeline(device, swapChainFormat, vertexLayouts, CullMode.None, PrimitiveTopology.TriangleList);
            _pipelineCull = CreateRenderPipeline(device, swapChainFormat, vertexLayouts, CullMode.Back, PrimitiveTopology.TriangleList);
            _wireframePipeline = CreateRenderPipeline(device, swapChainFormat, vertexLayouts, CullMode.None, PrimitiveTopology.LineList);
        }

        private ShaderModule* CreateShaderModule(Device* device, string source)
        {
            var sourceBytes = System.Text.Encoding.UTF8.GetBytes(source + "\0");

            fixed (byte* pSource = sourceBytes)
            {
                var wgslDescriptor = new ShaderModuleWGSLDescriptor
                {
                    Chain = new ChainedStruct
                    {
                        SType = SType.ShaderModuleWgslDescriptor
                    },
                    Code = pSource
                };

                var descriptor = new ShaderModuleDescriptor
                {
                    NextInChain = (ChainedStruct*)&wgslDescriptor
                };

                return _wgpu.DeviceCreateShaderModule(device, &descriptor);
            }
        }

        private void CreateUniformResources(Device* device)
        {
            var bufferSize = (ulong)sizeof(BasicModelUniforms);
            var alignedSize = (bufferSize + 255) & ~255ul;

            var bufferDesc = new BufferDescriptor
            {
                Size = alignedSize,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                MappedAtCreation = false
            };

            _uniformBuffer = _wgpu.DeviceCreateBuffer(device, &bufferDesc);

            var layoutEntry = new BindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
                Buffer = new BufferBindingLayout
                {
                    Type = BufferBindingType.Uniform,
                    MinBindingSize = 0
                }
            };

            var layoutDesc = new BindGroupLayoutDescriptor
            {
                EntryCount = 1,
                Entries = &layoutEntry
            };

            _uniformBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(device, &layoutDesc);

            var bindEntry = new BindGroupEntry
            {
                Binding = 0,
                Buffer = _uniformBuffer,
                Offset = 0,
                Size = (ulong)sizeof(BasicModelUniforms)
            };

            var bindGroupDesc = new BindGroupDescriptor
            {
                Layout = _uniformBindGroupLayout,
                EntryCount = 1,
                Entries = &bindEntry
            };

            _uniformBindGroup = _wgpu.DeviceCreateBindGroup(device, &bindGroupDesc);
        }

        private RenderPipeline* CreateRenderPipeline(
            Device* device,
            TextureFormat swapChainFormat,
            VertexBufferLayout[] vertexLayouts,
            CullMode cullMode,
            PrimitiveTopology topology)
        {
            var bindGroupLayout = _uniformBindGroupLayout;
            var pipelineLayoutDesc = new PipelineLayoutDescriptor
            {
                BindGroupLayoutCount = 1,
                BindGroupLayouts = &bindGroupLayout
            };

            var pipelineLayout = _wgpu.DeviceCreatePipelineLayout(device, &pipelineLayoutDesc);

            var vertexEntry = "vs_main\0"u8.ToArray();
            var fragmentEntry = "fs_main\0"u8.ToArray();

            fixed (VertexBufferLayout* pVertexLayouts = vertexLayouts)
            fixed (byte* pVertexEntry = vertexEntry)
            fixed (byte* pFragmentEntry = fragmentEntry)
            {
                var vertexState = new VertexState
                {
                    Module = _vertexModule,
                    EntryPoint = pVertexEntry,
                    BufferCount = (uint)vertexLayouts.Length,
                    Buffers = pVertexLayouts
                };

                var blendState = new BlendState
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

                var colorTargetState = new ColorTargetState
                {
                    Format = swapChainFormat,
                    WriteMask = ColorWriteMask.All,
                    Blend = &blendState
                };

                var fragmentState = new FragmentState
                {
                    Module = _fragmentModule,
                    EntryPoint = pFragmentEntry,
                    TargetCount = 1,
                    Targets = &colorTargetState
                };

                var depthStencilState = new DepthStencilState
                {
                    Format = TextureFormat.Depth24Plus,
                    DepthWriteEnabled = true,
                    DepthCompare = CompareFunction.Less,
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

                var primitiveState = new PrimitiveState
                {
                    Topology = topology,
                    FrontFace = FrontFace.CW,
                    CullMode = cullMode
                };

                var multisampleState = new MultisampleState
                {
                    Count = 1,
                    Mask = ~0u,
                    AlphaToCoverageEnabled = false
                };

                var pipelineDesc = new RenderPipelineDescriptor
                {
                    Layout = pipelineLayout,
                    Vertex = vertexState,
                    Fragment = &fragmentState,
                    DepthStencil = &depthStencilState,
                    Primitive = primitiveState,
                    Multisample = multisampleState
                };

                return _wgpu.DeviceCreateRenderPipeline(device, &pipelineDesc);
            }
        }

        public RenderPipeline* GetPipeline(bool cullBackface, bool wireframe)
        {
            if (wireframe)
            {
                return _wireframePipeline;
            }
            else
            {
                return cullBackface ? _pipelineCull : _pipeline;
            }
        }

        public void UpdateUniforms(BasicModelUniforms uniforms)
        {
            _wgpu.QueueWriteBuffer(_queue, _uniformBuffer, 0, uniforms, (nuint)sizeof(BasicModelUniforms));
        }

        public void Dispose()
        {
            if (_uniformBindGroup != null)
                _wgpu.BindGroupRelease(_uniformBindGroup);

            if (_uniformBuffer != null)
                _wgpu.BufferRelease(_uniformBuffer);

            if (_uniformBindGroupLayout != null)
                _wgpu.BindGroupLayoutRelease(_uniformBindGroupLayout);

            if (_pipeline != null)
                _wgpu.RenderPipelineRelease(_pipeline);

            if (_pipelineCull != null)
                _wgpu.RenderPipelineRelease(_pipelineCull);

            if (_wireframePipeline != null)
                _wgpu.RenderPipelineRelease(_wireframePipeline);

            if (_vertexModule != null)
                _wgpu.ShaderModuleRelease(_vertexModule);
        }
    }
}
