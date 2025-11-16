using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

namespace XNOEdit.Renderer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct GridUniforms
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector3 CameraPos;
        public float FadeStart;
        public float FadeEnd;
    }

    public unsafe class GridRenderer : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;
        private readonly Queue* _queue;
        private ShaderModule* _shaderModule;
        private RenderPipeline* _pipeline;
        private BindGroupLayout* _bindGroupLayout;
        private BindGroup* _bindGroup;
        private WgpuBuffer<GridUniforms> _uniformBuffer;
        private WgpuBuffer<float> _vertexBuffer;
        private int _lineCount;

        public GridRenderer(WebGPU wgpu, Device* device, Queue* queue, TextureFormat swapChainFormat, float size = 100.0f, int divisions = 100)
        {
            _wgpu = wgpu;
            _device = device;
            _queue = queue;

            CreateGrid(size, divisions);
            CreateShaderAndPipeline(swapChainFormat);
        }

        private void CreateGrid(float size, int divisions)
        {
            var vertices = new List<float>();
            var step = size / divisions;
            var halfSize = size / 2.0f;

            // Create grid lines parallel to X axis
            for (var i = 0; i <= divisions; i++)
            {
                var z = -halfSize + i * step;

                Vector3 color;
                if (i == divisions / 2)
                    color = new Vector3(0.4f, 0.6f, 1.0f);
                else if (i % 10 == 0)
                    color = new Vector3(0.5f, 0.5f, 0.5f);
                else
                    color = new Vector3(0.3f, 0.3f, 0.3f);

                vertices.AddRange([-halfSize, 0.0f, z, color.X, color.Y, color.Z]);
                vertices.AddRange([halfSize, 0.0f, z, color.X, color.Y, color.Z]);
            }

            // Create grid lines parallel to Z axis
            for (var i = 0; i <= divisions; i++)
            {
                var x = -halfSize + i * step;

                Vector3 color;
                if (i == divisions / 2)
                    color = new Vector3(1.0f, 0.4f, 0.4f);
                else if (i % 10 == 0)
                    color = new Vector3(0.5f, 0.5f, 0.5f);
                else
                    color = new Vector3(0.3f, 0.3f, 0.3f);

                vertices.AddRange([x, 0.0f, -halfSize, color.X, color.Y, color.Z]);
                vertices.AddRange([x, 0.0f, halfSize, color.X, color.Y, color.Z]);
            }

            _lineCount = (divisions + 1) * 2 * 2;
            _vertexBuffer = new WgpuBuffer<float>(_wgpu, _device, vertices.ToArray(), BufferUsage.Vertex);
        }

        private void CreateShaderAndPipeline(TextureFormat swapChainFormat)
        {
            var gridWgsl = EmbeddedResources.ReadAllText("XNOEdit/Shaders/Grid.wgsl");
            var shaderBytes = System.Text.Encoding.UTF8.GetBytes(gridWgsl + "\0");

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

            _uniformBuffer = WgpuBuffer<GridUniforms>.CreateUniform(_wgpu, _device);

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
                Buffer = _uniformBuffer.Handle,
                Offset = 0,
                Size = _uniformBuffer.Size
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

            var vertexAttrib = stackalloc VertexAttribute[2];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0, ShaderLocation = 0 };
            vertexAttrib[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };

            var vbLayout = new VertexBufferLayout
            {
                ArrayStride = 24,
                StepMode = VertexStepMode.Vertex,
                AttributeCount = 2,
                Attributes = vertexAttrib
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

            var colorTarget = new ColorTargetState
            {
                Format = swapChainFormat,
                WriteMask = ColorWriteMask.All,
                Blend = &blendState
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
                        Topology = PrimitiveTopology.LineList,
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
            Matrix4x4 model,
            Vector3 cameraPos,
            float fadeDistance)
        {
            var uniforms = new GridUniforms
            {
                Model = model,
                View = view,
                Projection = projection,
                CameraPos = cameraPos,
                FadeStart = fadeDistance * 0.6f,
                FadeEnd = fadeDistance
            };

            _uniformBuffer.UpdateData(_queue, in uniforms);
            _wgpu.RenderPassEncoderSetPipeline(passEncoder, _pipeline);
            uint dynamicOffset = 0;
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, _bindGroup, 0, &dynamicOffset);
            _wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            _wgpu.RenderPassEncoderDraw(passEncoder, (uint)_lineCount, 1, 0, 0);
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _uniformBuffer?.Dispose();

            if (_bindGroup != null)
                _wgpu.BindGroupRelease(_bindGroup);

            if (_bindGroupLayout != null)
                _wgpu.BindGroupLayoutRelease(_bindGroupLayout);

            if (_pipeline != null)
                _wgpu.RenderPipelineRelease(_pipeline);

            if (_shaderModule != null)
                _wgpu.ShaderModuleRelease(_shaderModule);
        }
    }
}
