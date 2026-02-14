using System.Numerics;
using System.Runtime.InteropServices;
using Solaris.Builders;
using Solaris.RHI;

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

    public unsafe class ModelShader : Shader
    {
        private SlBuffer<PerFrameUniforms>? _perFrameUniformBuffer;
        private SlBindGroup _perFrameBindGroup;

        private SlSampler _sampler;
        private SlTexture _defaultTexture;
        private SlTextureView _defaultTextureView;

        private SlBindGroupLayout _perFrameLayout;
        private SlBindGroupLayout _perMeshLayout;
        private SlBindGroupLayout _textureLayout;
        private SlBindGroup _defaultTextureBindGroup;

        public ModelShader(
            SlDevice device,
            string shaderSource)
            : base(
                device,
                shaderSource,
                "Model Shader",
                pipelineVariants: new Dictionary<string, SlPipelineVariantDescriptor>
                {
                    ["default"] = new()
                    {
                        Topology = SlPrimitiveTopology.TriangleList,
                        CullMode = SlCullMode.None,
                        FrontFace = SlFrontFace.CounterClockwise,
                        DepthWrite = true,
                        DepthCompare = SlCompareFunction.Greater,
                        AlphaBlend = true
                    },
                    ["culled"] = new()
                    {
                        Topology = SlPrimitiveTopology.TriangleList,
                        CullMode = SlCullMode.Back,
                        FrontFace = SlFrontFace.CounterClockwise,
                        DepthWrite = true,
                        DepthCompare = SlCompareFunction.Greater,
                        AlphaBlend = true
                    },
                    ["wireframe"] = new()
                    {
                        Topology = SlPrimitiveTopology.LineList,
                        CullMode = SlCullMode.None,
                        FrontFace = SlFrontFace.CounterClockwise,
                        DepthWrite = true,
                        DepthCompare = SlCompareFunction.Greater,
                        AlphaBlend = false
                    }
                })
        {
        }

        private void CreateSampler()
        {
            var samplerDesc = new SlSamplerDescriptor
            {
                AddressModeU = SlAddressMode.Repeat,
                AddressModeV = SlAddressMode.Repeat,
                AddressModeW = SlAddressMode.Repeat,
                MagFilter = SlFilterMode.Linear,
                MinFilter = SlFilterMode.Linear,
                MipmapFilter = SlFilterMode.Linear,
                LodMinClamp = 0.0f,
                LodMaxClamp = 32.0f,
                MaxAnisotropy = 16
            };

            _sampler = Device.CreateSampler(samplerDesc);
        }

        private void CreateDefaultTexture()
        {
            var textureDesc = new SlTextureDescriptor
            {
                Size = new SlExtent3D { Width = 1, Height = 1, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = SlTextureDimension.Dimension2D,
                Format = SlTextureFormat.Rgba8Unorm,
                Usage = SlTextureUsage.TextureBinding | SlTextureUsage.CopyDst
            };

            _defaultTexture = Device.CreateTexture(textureDesc);

            Span<byte> whitePixel = [255, 255, 255, 255];

            var imageCopyTexture = new SlCopyTextureDescriptor
            {
                Texture = _defaultTexture,
                MipLevel = 0,
                Origin = new SlOrigin3D { X = 0, Y = 0, Z = 0 },
            };

            var textureDataLayout = new SlTextureDataLayout
            {
                Offset = 0,
                BytesPerRow = 4,
                RowsPerImage = 1
            };

            var writeSize = new SlExtent3D { Width = 1, Height = 1, DepthOrArrayLayers = 1 };

            var queue = Device.GetQueue();
            queue.WriteTexture(imageCopyTexture, whitePixel, textureDataLayout, writeSize);
            queue.Dispose();

            var viewDesc = new SlTextureViewDescriptor
            {
                Format = SlTextureFormat.Rgba8Unorm,
                Dimension = SlTextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1
            };

            _defaultTextureView = _defaultTexture.CreateTextureView(viewDesc);
        }

        protected override void SetupBindGroups()
        {
            CreateSampler();
            CreateDefaultTexture();

            // Group 0: Per-frame uniforms (updated every frame)
            _perFrameUniformBuffer = Device.CreateUniform<PerFrameUniforms>();
            RegisterResource(_perFrameUniformBuffer);

            var perFrameBuilder = new BindGroupBuilder(Device);
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

        private SlBindGroupLayout CreatePerMeshBindGroupLayout()
        {
            var entry = new SlBindGroupLayoutEntry
            {
                Binding = 0,
                Type = SlBindingType.Buffer,
                Visibility = SlShaderStage.Vertex | SlShaderStage.Fragment,
                BufferType = SlBufferBindingType.Uniform
            };

            var layoutDesc = new SlBindGroupLayoutDescriptor
            {
                Entries = [entry]
            };

            return Device.CreateBindGroupLayout(layoutDesc);
        }

        public SlBindGroup CreatePerMeshBindGroup(SlBuffer<PerMeshUniforms> uniformBuffer)
        {
            var layout = GetBindGroupLayout(1);

            var entry = new SlBindGroupEntry
            {
                Binding = 0,
                Buffer = new SlBufferBinding
                {
                    Handle = uniformBuffer.GetHandle(),
                    Offset = 0,
                    Size = uniformBuffer.Size
                }
            };

            var bindGroupDesc = new SlBindGroupDescriptor
            {
                Layout = layout,
                Entries = [entry]
            };

            return Device.CreateBindGroup(bindGroupDesc);
        }

        private SlBindGroupLayout CreateTextureBindGroupLayout()
        {
            var entries = new SlBindGroupLayoutEntry[5];

            entries[0] = new SlBindGroupLayoutEntry
            {
                Binding = 0,
                Visibility = SlShaderStage.Fragment,
                Type = SlBindingType.Sampler,
                SamplerType = SlSamplerBindingType.Filtering
            };

            entries[1] = new SlBindGroupLayoutEntry
            {
                Binding = 1,
                Visibility = SlShaderStage.Fragment,
                Type = SlBindingType.Texture,
                TextureSampleType = SlTextureSampleType.Float,
                TextureDimension = SlTextureViewDimension.Dimension2D
            };

            entries[2] = new SlBindGroupLayoutEntry
            {
                Binding = 2,
                Visibility = SlShaderStage.Fragment,
                Type = SlBindingType.Texture,
                TextureSampleType = SlTextureSampleType.Float,
                TextureDimension = SlTextureViewDimension.Dimension2D
            };

            entries[3] = new SlBindGroupLayoutEntry
            {
                Binding = 3,
                Visibility = SlShaderStage.Fragment,
                Type = SlBindingType.Texture,
                TextureSampleType = SlTextureSampleType.Float,
                TextureDimension = SlTextureViewDimension.Dimension2D
            };

            entries[4] = new SlBindGroupLayoutEntry
            {
                Binding = 4,
                Visibility = SlShaderStage.Fragment,
                Type = SlBindingType.Texture,
                TextureSampleType = SlTextureSampleType.Float,
                TextureDimension = SlTextureViewDimension.Dimension2D
            };

            var layoutDesc = new SlBindGroupLayoutDescriptor
            {
                Entries = entries
            };

            return Device.CreateBindGroupLayout(layoutDesc);
        }

        public SlBindGroup CreateTextureBindGroup(
            SlBindGroupLayout layout,
            SlTextureView? mainTextureView,
            SlTextureView? blendTextureView,
            SlTextureView? normalTextureView,
            SlTextureView? lightmapTextureView)
        {
            var entries = new SlBindGroupEntry[5];

            entries[0] = new SlBindGroupEntry
            {
                Binding = 0,
                Sampler = _sampler
            };

            entries[1] = new SlBindGroupEntry
            {
                Binding = 1,
                TextureView = mainTextureView ?? _defaultTextureView
            };

            entries[2] = new SlBindGroupEntry
            {
                Binding = 2,
                TextureView = blendTextureView ?? _defaultTextureView
            };

            entries[3] = new SlBindGroupEntry
            {
                Binding = 3,
                TextureView = normalTextureView ?? _defaultTextureView
            };

            entries[4] = new SlBindGroupEntry
            {
                Binding = 4,
                TextureView = lightmapTextureView ?? _defaultTextureView
            };

            var bindGroupDesc = new SlBindGroupDescriptor
            {
                Layout = layout,
                Entries = entries
            };

            return Device.CreateBindGroup(bindGroupDesc);
        }

        protected override SlVertexBufferLayout[] CreateVertexLayouts()
        {
            var vertexAttrib = new SlVertexAttribute[5];
            vertexAttrib[0] = new SlVertexAttribute { Format = SlVertexFormat.Float32x3, Offset = 0,  ShaderLocation = 0 };
            vertexAttrib[1] = new SlVertexAttribute { Format = SlVertexFormat.Float32x3, Offset = 12, ShaderLocation = 1 };
            vertexAttrib[2] = new SlVertexAttribute { Format = SlVertexFormat.Float32x4, Offset = 24, ShaderLocation = 2 };
            vertexAttrib[3] = new SlVertexAttribute { Format = SlVertexFormat.Float32x2, Offset = 40, ShaderLocation = 3 };
            vertexAttrib[4] = new SlVertexAttribute { Format = SlVertexFormat.Float32x2, Offset = 48, ShaderLocation = 4 };

            return
            [
                new SlVertexBufferLayout
                {
                    Stride = 56,
                    StepMode = SlVertexStepMode.Vertex,
                    Attributes = vertexAttrib
                }
            ];
        }

        public void UpdatePerFrameUniforms(SlQueue queue, in PerFrameUniforms uniforms)
        {
            _perFrameUniformBuffer?.UpdateData(queue, in uniforms);
        }

        public SlBindGroup GetTextureBindGroup(
            SlTextureView mainTextureView,
            SlTextureView blendTextureView,
            SlTextureView normalTextureView,
            SlTextureView lightmapTextureView)
        {
            var layout = GetBindGroupLayout(2);
            return CreateTextureBindGroup(layout, mainTextureView, blendTextureView, normalTextureView, lightmapTextureView);
        }

        public SlRenderPipeline GetPipeline(bool cullBackfaces, bool wireframe)
        {
            if (wireframe)
                return GetPipeline("wireframe");

            return cullBackfaces ? GetPipeline("culled") : GetPipeline();
        }

        public override void Dispose()
        {
            _sampler?.Dispose();
            _defaultTextureView?.Dispose();
            _defaultTexture?.Dispose();

            base.Dispose();
        }
    }
}
