using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace XNOEdit.Renderer
{
    public unsafe class WgpuShader : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Queue* _queue;
        private readonly ShaderModule* _vertexModule;
        private readonly ShaderModule* _fragmentModule;
        private readonly RenderPipeline* _pipeline;
        private readonly RenderPipeline* _pipelineCull;
        private readonly RenderPipeline* _wireframePipeline;
        private BindGroupLayout* _uniformBindGroupLayout;
        private WgpuBuffer<BasicModelUniforms> _uniformBuffer;
        private BindGroup* _uniformBindGroup;

        public BindGroup* UniformBindGroup => _uniformBindGroup;

        public WgpuShader(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            string shaderSource,
            string label,
            TextureFormat swapChainFormat,
            VertexBufferLayout[] vertexLayouts)
        {
            _wgpu = wgpu;
            _queue = queue;

            _vertexModule = CreateShaderModule(device, shaderSource, label);
            _fragmentModule = _vertexModule;

            CreateUniformResources(device);

            _pipeline = CreateRenderPipeline(device, swapChainFormat, vertexLayouts, CullMode.None, PrimitiveTopology.TriangleList);
            _pipelineCull = CreateRenderPipeline(device, swapChainFormat, vertexLayouts, CullMode.Back, PrimitiveTopology.TriangleList);
            _wireframePipeline = CreateRenderPipeline(device, swapChainFormat, vertexLayouts, CullMode.None, PrimitiveTopology.LineList);
        }

        private ShaderModule* CreateShaderModule(Device* device, string source, string label)
        {
            var src = SilkMarshal.StringToPtr(source);
            var shaderName = SilkMarshal.StringToPtr(label);

            var wgslDescriptor = new ShaderModuleWGSLDescriptor
            {
                Chain = new ChainedStruct
                {
                    SType = SType.ShaderModuleWgslDescriptor
                },
                Code = (byte*)src
            };

            var descriptor = new ShaderModuleDescriptor
            {
                Label = (byte*)shaderName,
                NextInChain = (ChainedStruct*)&wgslDescriptor
            };

            var shader = _wgpu.DeviceCreateShaderModule(device, &descriptor);

            SilkMarshal.Free(src);
            SilkMarshal.Free(shaderName);

            return shader;
        }

        private void CreateUniformResources(Device* device)
        {
            _uniformBuffer = WgpuBuffer<BasicModelUniforms>.CreateUniform(_wgpu, device);

            var layoutEntry = WgpuBuffer<BasicModelUniforms>.CreateLayoutEntry(
                binding: 0,
                visibility: ShaderStage.Vertex | ShaderStage.Fragment);

            var layoutDesc = new BindGroupLayoutDescriptor
            {
                EntryCount = 1,
                Entries = &layoutEntry
            };

            _uniformBindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(device, &layoutDesc);

            var bindEntry = _uniformBuffer.CreateBindGroupEntry(binding: 0);

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

            var pipelineLayout = _wgpu.DeviceCreatePipelineLayout(device, &pipelineLayoutDesc);

            var vertexEntry = SilkMarshal.StringToPtr("vs_main");
            var fragmentEntry = SilkMarshal.StringToPtr("fs_main");

            VertexState vertexState;
            FragmentState fragmentState;

            fixed (VertexBufferLayout* pVertexLayouts = vertexLayouts)
            {
                vertexState = new VertexState
                {
                    Module = _vertexModule,
                    EntryPoint = (byte*)vertexEntry,
                    BufferCount = (uint)vertexLayouts.Length,
                    Buffers = pVertexLayouts
                };

                fragmentState = new FragmentState
                {
                    Module = _fragmentModule,
                    EntryPoint = (byte*)fragmentEntry,
                    TargetCount = 1,
                    Targets = &colorTargetState
                };
            }

            var pipelineDesc = new RenderPipelineDescriptor
            {
                Layout = pipelineLayout,
                Vertex = vertexState,
                Fragment = &fragmentState,
                DepthStencil = &depthStencilState,
                Primitive = primitiveState,
                Multisample = multisampleState
            };

            var pipeline = _wgpu.DeviceCreateRenderPipeline(device, &pipelineDesc);

            SilkMarshal.Free(vertexEntry);
            SilkMarshal.Free(fragmentEntry);

            return pipeline;
        }

        public RenderPipeline* GetPipeline(bool cullBackface, bool wireframe)
        {
            if (wireframe)
            {
                return _wireframePipeline;
            }

            return cullBackface ? _pipelineCull : _pipeline;
        }

        public void UpdateUniforms(in BasicModelUniforms uniforms)
        {
            _uniformBuffer.UpdateData(_queue, in uniforms);
        }

        public void Dispose()
        {
            _uniformBuffer?.Dispose();

            if (_uniformBindGroup != null)
                _wgpu.BindGroupRelease(_uniformBindGroup);

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
