using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Builders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer.Shaders
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct PerFrameUniforms
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 SunDirection;
        public Vector4 SunColor;
        public Vector3 CameraPosition;
        public float VertColorStrength;
        public float Lightmap;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct PerMeshUniforms
    {
        public Vector4 AmbientColor;
        public Vector4 DiffuseColor;
        public Vector4 SpecularColor;
        public Vector4 EmissiveColor;
        public float SpecularPower;
        public float AlphaRef;
        public float Alpha;
        public float Blend;
        public float Specular;
    }

    public unsafe class ModelShader : WgpuShader
    {
        private WgpuBuffer<PerFrameUniforms>? _perFrameUniformBuffer;
        private BindGroup* _perFrameBindGroup;

        private Sampler* _sampler;
        private Texture* _defaultTexture;
        private TextureView* _defaultTextureView;

        private BindGroupLayout* _perFrameLayout;
        private BindGroupLayout* _perMeshLayout;
        private BindGroupLayout* _textureLayout;
        private BindGroup* _defaultTextureBindGroup;

        public ModelShader(
            WebGPU wgpu,
            WgpuDevice device,
            string shaderSource)
            : base(
                wgpu,
                device,
                shaderSource,
                "Model Shader",
                pipelineVariants: new Dictionary<string, PipelineVariantDescriptor>
                {
                    ["default"] = new()
                    {
                        Topology = PrimitiveTopology.TriangleList,
                        CullMode = CullMode.None,
                        FrontFace = FrontFace.Ccw,
                        DepthWrite = true,
                        DepthCompare = CompareFunction.Greater,
                        AlphaBlend = true
                    },
                    ["culled"] = new()
                    {
                        Topology = PrimitiveTopology.TriangleList,
                        CullMode = CullMode.Back,
                        FrontFace = FrontFace.Ccw,
                        DepthWrite = true,
                        DepthCompare = CompareFunction.Greater,
                        AlphaBlend = true
                    },
                    ["wireframe"] = new()
                    {
                        Topology = PrimitiveTopology.LineList,
                        CullMode = CullMode.None,
                        FrontFace = FrontFace.Ccw,
                        DepthWrite = true,
                        DepthCompare = CompareFunction.Greater,
                        AlphaBlend = false
                    }
                })
        {
        }

        private void CreateSampler()
        {
            var samplerDesc = new SamplerDescriptor
            {
                AddressModeU = AddressMode.Repeat,
                AddressModeV = AddressMode.Repeat,
                AddressModeW = AddressMode.Repeat,
                MagFilter = FilterMode.Linear,
                MinFilter = FilterMode.Linear,
                MipmapFilter = MipmapFilterMode.Linear,
                LodMinClamp = 0.0f,
                LodMaxClamp = 32.0f,
                MaxAnisotropy = 16
            };

            _sampler = Wgpu.DeviceCreateSampler(Device, &samplerDesc);
        }

        private void CreateDefaultTexture()
        {
            var textureDesc = new TextureDescriptor
            {
                Size = new Extent3D { Width = 1, Height = 1, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = TextureFormat.Rgba8Unorm,
                Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
            };

            _defaultTexture = Wgpu.DeviceCreateTexture(Device, &textureDesc);

            var whitePixel = stackalloc byte[4] { 255, 255, 255, 255 };

            var imageCopyTexture = new ImageCopyTexture
            {
                Texture = _defaultTexture,
                MipLevel = 0,
                Origin = new Origin3D { X = 0, Y = 0, Z = 0 },
                Aspect = TextureAspect.All
            };

            var textureDataLayout = new TextureDataLayout
            {
                Offset = 0,
                BytesPerRow = 4,
                RowsPerImage = 1
            };

            var writeSize = new Extent3D { Width = 1, Height = 1, DepthOrArrayLayers = 1 };

            var queue = Wgpu.DeviceGetQueue(Device);
            Wgpu.QueueWriteTexture(queue, &imageCopyTexture, whitePixel, 4, &textureDataLayout, &writeSize);
            Wgpu.QueueRelease(queue);

            var viewDesc = new TextureViewDescriptor
            {
                Format = TextureFormat.Rgba8Unorm,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            _defaultTextureView = Wgpu.TextureCreateView(_defaultTexture, &viewDesc);
        }

        protected override void SetupBindGroups()
        {
            CreateSampler();
            CreateDefaultTexture();

            // Group 0: Per-frame uniforms (updated every frame)
            _perFrameUniformBuffer = WgpuBuffer<PerFrameUniforms>.CreateUniform(Wgpu, Device);
            RegisterResource(_perFrameUniformBuffer);

            var perFrameBuilder = new BindGroupBuilder(Wgpu, Device);
            perFrameBuilder.AddUniformBuffer(0, _perFrameUniformBuffer);

            _perFrameLayout = perFrameBuilder.BuildLayout();
            _perFrameBindGroup = perFrameBuilder.BuildBindGroup();

            RegisterBindGroup(_perFrameLayout, _perFrameBindGroup);

            // Group 1: Per-mesh uniforms (created per mesh, set once)
            // We only create the layout here; meshes will create their own bind groups
            _perMeshLayout = CreatePerMeshBindGroupLayout();
            RegisterBindGroup(_perMeshLayout, null);

            // Group 2: Textures (created per draw call)
            _textureLayout = CreateTextureBindGroupLayout();
            _defaultTextureBindGroup = CreateTextureBindGroup(_textureLayout, null, null, null, null);
            RegisterBindGroup(_textureLayout, _defaultTextureBindGroup);
        }

        private BindGroupLayout* CreatePerMeshBindGroupLayout()
        {
            var entry = new BindGroupLayoutEntry
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
                Entries = &entry
            };

            return Wgpu.DeviceCreateBindGroupLayout(Device, &layoutDesc);
        }

        public BindGroup* CreatePerMeshBindGroup(WgpuBuffer<PerMeshUniforms> uniformBuffer)
        {
            var layout = GetBindGroupLayout(1);

            var entry = new BindGroupEntry
            {
                Binding = 0,
                Buffer = uniformBuffer.Handle,
                Offset = 0,
                Size = uniformBuffer.Size
            };

            var bindGroupDesc = new BindGroupDescriptor
            {
                Layout = layout,
                EntryCount = 1,
                Entries = &entry
            };

            return Wgpu.DeviceCreateBindGroup(Device, &bindGroupDesc);
        }

        private BindGroupLayout* CreateTextureBindGroupLayout()
        {
            var entries = stackalloc BindGroupLayoutEntry[5];

            entries[0] = new BindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = ShaderStage.Fragment,
                Sampler = new SamplerBindingLayout
                {
                    Type = SamplerBindingType.Filtering
                }
            };

            entries[1] = new BindGroupLayoutEntry
            {
                Binding = 1,
                Visibility = ShaderStage.Fragment,
                Texture = new TextureBindingLayout
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D,
                    Multisampled = false
                }
            };

            entries[2] = new BindGroupLayoutEntry
            {
                Binding = 2,
                Visibility = ShaderStage.Fragment,
                Texture = new TextureBindingLayout
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D,
                    Multisampled = false
                }
            };

            entries[3] = new BindGroupLayoutEntry
            {
                Binding = 3,
                Visibility = ShaderStage.Fragment,
                Texture = new TextureBindingLayout
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D,
                    Multisampled = false
                }
            };

            entries[4] = new BindGroupLayoutEntry
            {
                Binding = 4,
                Visibility = ShaderStage.Fragment,
                Texture = new TextureBindingLayout
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D,
                    Multisampled = false
                }
            };

            var layoutDesc = new BindGroupLayoutDescriptor
            {
                EntryCount = 5,
                Entries = entries
            };

            return Wgpu.DeviceCreateBindGroupLayout(Device, &layoutDesc);
        }

        public BindGroup* CreateTextureBindGroup(
            BindGroupLayout* layout,
            TextureView* mainTextureView,
            TextureView* blendTextureView,
            TextureView* normalTextureView,
            TextureView* lightmapTextureView)
        {
            var entries = stackalloc BindGroupEntry[5];

            entries[0] = new BindGroupEntry
            {
                Binding = 0,
                Sampler = _sampler
            };

            entries[1] = new BindGroupEntry
            {
                Binding = 1,
                TextureView = mainTextureView == null ? _defaultTextureView : mainTextureView
            };

            entries[2] = new BindGroupEntry
            {
                Binding = 2,
                TextureView = blendTextureView == null ? _defaultTextureView : blendTextureView
            };

            entries[3] = new BindGroupEntry
            {
                Binding = 3,
                TextureView = normalTextureView == null ? _defaultTextureView : normalTextureView
            };

            entries[4] = new BindGroupEntry
            {
                Binding = 4,
                TextureView = lightmapTextureView == null ? _defaultTextureView : lightmapTextureView
            };

            var bindGroupDesc = new BindGroupDescriptor
            {
                Layout = layout,
                EntryCount = 5,
                Entries = entries
            };

            return Wgpu.DeviceCreateBindGroup(Device, &bindGroupDesc);
        }

        protected override VertexBufferLayout[] CreateVertexLayouts()
        {
            var vertexAttrib = new VertexAttribute[5];
            vertexAttrib[0] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 0,  ShaderLocation = 0 };
            vertexAttrib[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };
            vertexAttrib[2] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 24, ShaderLocation = 2 };
            vertexAttrib[3] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 40, ShaderLocation = 3 };
            vertexAttrib[4] = new VertexAttribute { Format = VertexFormat.Float32x2, Offset = 48, ShaderLocation = 4 };

            Attributes = GCHandle.Alloc(vertexAttrib, GCHandleType.Pinned);

            return
            [
                new VertexBufferLayout
                {
                    ArrayStride = 56,
                    StepMode = VertexStepMode.Vertex,
                    AttributeCount = (uint)vertexAttrib.Length,
                    Attributes = (VertexAttribute*)Attributes.AddrOfPinnedObject()
                }
            ];
        }

        public void UpdatePerFrameUniforms(Queue* queue, in PerFrameUniforms uniforms)
        {
            _perFrameUniformBuffer?.UpdateData(queue, in uniforms);
        }

        public BindGroup* GetTextureBindGroup(
            TextureView* mainTextureView,
            TextureView* blendTextureView,
            TextureView* normalTextureView,
            TextureView* lightmapTextureView)
        {
            var layout = GetBindGroupLayout(2);
            return CreateTextureBindGroup(layout, mainTextureView, blendTextureView, normalTextureView, lightmapTextureView);
        }

        public RenderPipeline* GetPipeline(bool cullBackfaces, bool wireframe)
        {
            if (wireframe)
                return GetPipeline("wireframe");

            return cullBackfaces ? GetPipeline("culled") : GetPipeline();
        }

        public override void Dispose()
        {
            if (_sampler != null)
                Wgpu.SamplerRelease(_sampler);

            if (_defaultTextureView != null)
                Wgpu.TextureViewRelease(_defaultTextureView);

            if (_defaultTexture != null)
            {
                Wgpu.TextureDestroy(_defaultTexture);
                Wgpu.TextureRelease(_defaultTexture);
            }

            base.Dispose();
        }
    }
}
