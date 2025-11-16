using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SkyboxUniforms
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 SunDirection;
        public Vector4 SunColor;
    }

    public unsafe class SkyboxRenderer : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;
        private readonly Queue* _queue;
        private ShaderModule* _shaderModule;
        private RenderPipeline* _pipeline;
        private BindGroupLayout* _bindGroupLayout;
        private BindGroup* _bindGroup;
        private Buffer* _uniformBuffer;
        private WgpuBuffer<float> _vertexBuffer;

        public SkyboxRenderer(WebGPU wgpu, Device* device, Queue* queue, TextureFormat swapChainFormat)
        {
            _wgpu = wgpu;
            _device = device;
            _queue = queue;

            CreateQuad();
            CreateShaderAndPipeline(swapChainFormat);
        }

        private void CreateQuad()
        {
            var vertices = new[]
            {
                -1.0f, -1.0f, 0.0f,
                 1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                 1.0f,  1.0f, 0.0f
            };

            _vertexBuffer = new WgpuBuffer<float>(_wgpu, _device, vertices, BufferUsage.Vertex);
        }

        private void CreateShaderAndPipeline(TextureFormat swapChainFormat)
        {
            var skyboxWgsl = EmbeddedResources.ReadAllText("XNOEdit/Shaders/Skybox.wgsl");
            var shaderBytes = System.Text.Encoding.UTF8.GetBytes(skyboxWgsl + "\0");

            fixed (byte* pShader = shaderBytes)
            {
                var wgslDesc = new ShaderModuleWGSLDescriptor
                {
                    Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor },
                    Code = pShader
                };

                var shaderDesc = new ShaderModuleDescriptor
                {
                    NextInChain = (ChainedStruct*)&wgslDesc
                };

                _shaderModule = _wgpu.DeviceCreateShaderModule(_device, &shaderDesc);
            }

            var bufferSize = 160ul;
            var alignedSize = (bufferSize + 255) & ~255ul;

            var bufferDesc = new BufferDescriptor
            {
                Size = alignedSize,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                MappedAtCreation = false
            };
            _uniformBuffer = _wgpu.DeviceCreateBuffer(_device, &bufferDesc);

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
            _bindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, &layoutDesc);

            var bindEntry = new BindGroupEntry
            {
                Binding = 0,
                Buffer = _uniformBuffer,
                Offset = 0,
                Size = 160
            };

            var bindGroupDesc = new BindGroupDescriptor
            {
                Layout = _bindGroupLayout,
                EntryCount = 1,
                Entries = &bindEntry
            };
            _bindGroup = _wgpu.DeviceCreateBindGroup(_device, &bindGroupDesc);

            var layouts = _bindGroupLayout;
            var pipelineLayoutDesc = new PipelineLayoutDescriptor
            {
                BindGroupLayoutCount = 1,
                BindGroupLayouts = &layouts
            };
            var pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, &pipelineLayoutDesc);

            var vertexEntry = "vs_main\0"u8.ToArray();
            var fragmentEntry = "fs_main\0"u8.ToArray();

            VertexAttribute* vertexAttrib = stackalloc VertexAttribute[1];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 };

            var vbLayout = new VertexBufferLayout
            {
                ArrayStride = 12,
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 1,
                Attributes = vertexAttrib
            };

            var colorTarget = new ColorTargetState
            {
                Format = swapChainFormat,
                WriteMask = ColorWriteMask.All,
                Blend = null
            };

            fixed (byte* pVertexEntry = vertexEntry)
            fixed (byte* pFragmentEntry = fragmentEntry)
            {
                var fragmentState = new FragmentState
                {
                    Module = _shaderModule,
                    EntryPoint = pFragmentEntry,
                    TargetCount = 1,
                    Targets = &colorTarget
                };

                var depthStencil = new DepthStencilState
                {
                    Format = TextureFormat.Depth24Plus,
                    DepthWriteEnabled = false,
                    DepthCompare = CompareFunction.Always,
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

                var pipelineDesc = new RenderPipelineDescriptor
                {
                    Layout = pipelineLayout,
                    Vertex = new VertexState
                    {
                        Module = _shaderModule,
                        EntryPoint = pVertexEntry,
                        BufferCount = 1,
                        Buffers = &vbLayout
                    },
                    Primitive = new PrimitiveState
                    {
                        Topology = PrimitiveTopology.TriangleStrip,
                        FrontFace = FrontFace.Ccw,
                        CullMode = CullMode.None
                    },
                    DepthStencil = &depthStencil,
                    Multisample = new MultisampleState
                    {
                        Count = 1,
                        Mask = ~0u,
                        AlphaToCoverageEnabled = false
                    },
                    Fragment = &fragmentState
                };

                _pipeline = _wgpu.DeviceCreateRenderPipeline(_device, &pipelineDesc);
            }

            _wgpu.PipelineLayoutRelease(pipelineLayout);
        }

        public void Draw(
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            Vector3 sunDirection,
            Vector3 sunColor)
        {
            var uniforms = new SkyboxUniforms
            {
                View = view,
                Projection = projection,
                SunDirection = sunDirection.AsVector4(),
                SunColor = sunColor.AsVector4()
            };

            _wgpu.QueueWriteBuffer(_queue, _uniformBuffer, 0, in uniforms, (nuint)sizeof(SkyboxUniforms));
            _wgpu.RenderPassEncoderSetPipeline(passEncoder, _pipeline);
            uint dynamicOffset = 0;
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, _bindGroup, 0, &dynamicOffset);
            _wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            _wgpu.RenderPassEncoderDraw(passEncoder, 4, 1, 0, 0);
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();

            if (_bindGroup != null)
                _wgpu.BindGroupRelease(_bindGroup);

            if (_uniformBuffer != null)
                _wgpu.BufferRelease(_uniformBuffer);

            if (_bindGroupLayout != null)
                _wgpu.BindGroupLayoutRelease(_bindGroupLayout);

            if (_pipeline != null)
                _wgpu.RenderPipelineRelease(_pipeline);

            if (_shaderModule != null)
                _wgpu.ShaderModuleRelease(_shaderModule);
        }
    }
}
