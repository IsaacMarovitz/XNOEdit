using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal static class EnumConversion
    {
        public static BufferUsage Convert(this SlBufferUsage usage)
        {
            var result = BufferUsage.None;

            if (usage.HasFlag(SlBufferUsage.MapRead)) result |= BufferUsage.MapRead;
            if (usage.HasFlag(SlBufferUsage.MapWrite)) result |= BufferUsage.MapWrite;
            if (usage.HasFlag(SlBufferUsage.CopySrc)) result |= BufferUsage.CopySrc;
            if (usage.HasFlag(SlBufferUsage.CopyDst)) result |= BufferUsage.CopyDst;
            if (usage.HasFlag(SlBufferUsage.Index)) result |= BufferUsage.Index;
            if (usage.HasFlag(SlBufferUsage.Vertex)) result |= BufferUsage.Vertex;
            if (usage.HasFlag(SlBufferUsage.Uniform)) result |= BufferUsage.Uniform;
            if (usage.HasFlag(SlBufferUsage.Storage)) result |= BufferUsage.Storage;

            return result;
        }

        public static PrimitiveTopology Convert(this SlPrimitiveTopology topology)
        {
            return topology switch
            {
                SlPrimitiveTopology.PointList => PrimitiveTopology.PointList,
                SlPrimitiveTopology.LineList => PrimitiveTopology.LineList,
                SlPrimitiveTopology.LineStrip => PrimitiveTopology.LineStrip,
                SlPrimitiveTopology.TriangleList => PrimitiveTopology.TriangleList,
                SlPrimitiveTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
                _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
            };
        }

        public static CullMode Convert(this SlCullMode cullMode)
        {
            return cullMode switch
            {
                SlCullMode.None => CullMode.None,
                SlCullMode.Front => CullMode.Front,
                SlCullMode.Back => CullMode.Back,
                _ => throw new ArgumentOutOfRangeException(nameof(cullMode), cullMode, null)
            };
        }

        public static FrontFace Convert(this SlFrontFace frontFace)
        {
            return frontFace switch
            {
                SlFrontFace.Clockwise => FrontFace.CW,
                SlFrontFace.CounterClockwise => FrontFace.Ccw,
                _ => throw new ArgumentOutOfRangeException(nameof(frontFace), frontFace, null)
            };
        }

        public static CompareFunction Convert(this SlCompareFunction compareFunction)
        {
            return compareFunction switch
            {
                SlCompareFunction.Never => CompareFunction.Never,
                SlCompareFunction.Less => CompareFunction.Less,
                SlCompareFunction.LessEqual => CompareFunction.LessEqual,
                SlCompareFunction.Greater => CompareFunction.Greater,
                SlCompareFunction.GreaterEqual => CompareFunction.GreaterEqual,
                SlCompareFunction.Equal => CompareFunction.Equal,
                SlCompareFunction.NotEqual => CompareFunction.NotEqual,
                SlCompareFunction.Always => CompareFunction.Always,
                _ => throw new ArgumentOutOfRangeException(nameof(compareFunction), compareFunction, null)
            };
        }

        public static TextureDimension Convert(this SlTextureDimension textureDimension)
        {
            return textureDimension switch
            {
                SlTextureDimension.Dimension1D => TextureDimension.Dimension1D,
                SlTextureDimension.Dimension2D => TextureDimension.Dimension2D,
                SlTextureDimension.Dimension3D => TextureDimension.Dimension3D,
                _ => throw new ArgumentOutOfRangeException(nameof(textureDimension), textureDimension, null)
            };
        }

        public static TextureViewDimension Convert(this SlTextureViewDimension textureViewDimension)
        {
            return textureViewDimension switch
            {
                SlTextureViewDimension.DimensionUndefined => TextureViewDimension.DimensionUndefined,
                SlTextureViewDimension.Dimension1D => TextureViewDimension.Dimension1D,
                SlTextureViewDimension.Dimension2D => TextureViewDimension.Dimension2D,
                SlTextureViewDimension.Dimension2DArray => TextureViewDimension.Dimension2DArray,
                SlTextureViewDimension.DimensionCube => TextureViewDimension.DimensionCube,
                SlTextureViewDimension.DimensionCubeArray => TextureViewDimension.DimensionCubeArray,
                SlTextureViewDimension.Dimension3D => TextureViewDimension.Dimension3D,
                _ => throw new ArgumentOutOfRangeException(nameof(textureViewDimension), textureViewDimension, null)
            };
        }

        public static TextureUsage Convert(this SlTextureUsage usage)
        {
            var result = TextureUsage.None;

            if (usage.HasFlag(SlTextureUsage.CopySrc)) result |= TextureUsage.CopySrc;
            if (usage.HasFlag(SlTextureUsage.CopyDst)) result |= TextureUsage.CopyDst;
            if (usage.HasFlag(SlTextureUsage.TextureBinding)) result |= TextureUsage.TextureBinding;
            if (usage.HasFlag(SlTextureUsage.StorageBinding)) result |= TextureUsage.StorageBinding;
            if (usage.HasFlag(SlTextureUsage.RenderAttachment)) result |= TextureUsage.RenderAttachment;

            return result;
        }

        public static TextureFormat Convert(this SlTextureFormat format)
        {
            return format switch
            {
                SlTextureFormat.R8Unorm => TextureFormat.R8Unorm,
                SlTextureFormat.R8Snorm => TextureFormat.R8Snorm,
                SlTextureFormat.R8Uint => TextureFormat.R8Uint,
                SlTextureFormat.R8Sint => TextureFormat.R8Sint,
                SlTextureFormat.R16Uint => TextureFormat.R16Uint,
                SlTextureFormat.R16Sint => TextureFormat.R16Sint,
                SlTextureFormat.R16float => TextureFormat.R16float,
                SlTextureFormat.RG8Unorm => TextureFormat.RG8Unorm,
                SlTextureFormat.RG8Snorm => TextureFormat.RG8Snorm,
                SlTextureFormat.RG8Uint => TextureFormat.RG8Uint,
                SlTextureFormat.RG8Sint => TextureFormat.RG8Sint,
                SlTextureFormat.R32float => TextureFormat.R32float,
                SlTextureFormat.R32Uint => TextureFormat.R32Uint,
                SlTextureFormat.R32Sint => TextureFormat.R32Sint,
                SlTextureFormat.RG16Uint => TextureFormat.RG16Uint,
                SlTextureFormat.RG16Sint => TextureFormat.RG16Sint,
                SlTextureFormat.RG16float => TextureFormat.RG16float,
                SlTextureFormat.Rgba8Unorm => TextureFormat.Rgba8Unorm,
                SlTextureFormat.Rgba8UnormSrgb => TextureFormat.Rgba8UnormSrgb,
                SlTextureFormat.Rgba8Snorm => TextureFormat.Rgba8Snorm,
                SlTextureFormat.Rgba8Uint => TextureFormat.Rgba8Uint,
                SlTextureFormat.Rgba8Sint => TextureFormat.Rgba8Sint,
                SlTextureFormat.Bgra8Unorm => TextureFormat.Bgra8Unorm,
                SlTextureFormat.Bgra8UnormSrgb => TextureFormat.Bgra8UnormSrgb,
                SlTextureFormat.Rgba16Uint => TextureFormat.Rgba16Uint,
                SlTextureFormat.Rgba16Sint => TextureFormat.Rgba16Sint,
                SlTextureFormat.Rgba16float => TextureFormat.Rgba16float,
                SlTextureFormat.Rgba32float => TextureFormat.Rgba32float,
                SlTextureFormat.Rgba32Uint => TextureFormat.Rgba32Uint,
                SlTextureFormat.Rgba32Sint => TextureFormat.Rgba32Sint,
                SlTextureFormat.Stencil8 => TextureFormat.Stencil8,
                SlTextureFormat.Depth16Unorm => TextureFormat.Depth16Unorm,
                SlTextureFormat.Depth24Plus => TextureFormat.Depth24Plus,
                SlTextureFormat.Depth24PlusStencil8 => TextureFormat.Depth24PlusStencil8,
                SlTextureFormat.Depth32float => TextureFormat.Depth32float,
                SlTextureFormat.Depth32floatStencil8 => TextureFormat.Depth32floatStencil8,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }

        public static AddressMode Convert(this SlAddressMode addressMode)
        {
            return addressMode switch
            {
                SlAddressMode.Repeat => AddressMode.Repeat,
                SlAddressMode.MirrorRepeat => AddressMode.MirrorRepeat,
                SlAddressMode.ClampToEdge => AddressMode.ClampToEdge,
                _ => throw new ArgumentOutOfRangeException(nameof(addressMode), addressMode, null)
            };
        }

        public static FilterMode Convert(this SlFilterMode filterMode)
        {
            return filterMode switch
            {
                SlFilterMode.Nearest => FilterMode.Nearest,
                SlFilterMode.Linear => FilterMode.Linear,
                _ => throw new ArgumentOutOfRangeException(nameof(filterMode), filterMode, null)
            };
        }

        public static MipmapFilterMode MipmapConvert(this SlFilterMode filterMode)
        {
            return filterMode switch
            {
                SlFilterMode.Nearest => MipmapFilterMode.Nearest,
                SlFilterMode.Linear => MipmapFilterMode.Linear,
                _ => throw new ArgumentOutOfRangeException(nameof(filterMode), filterMode, null)
            };
        }

        public static PresentMode Convert(this SlPresentMode presentMode)
        {
            return presentMode switch
            {
                SlPresentMode.Fifo => PresentMode.Fifo,
                SlPresentMode.Immediate => PresentMode.Immediate,
                SlPresentMode.Mailbox => PresentMode.Mailbox,
                _ => throw new ArgumentOutOfRangeException(nameof(presentMode), presentMode, null)
            };
        }

        public static IndexFormat Convert(this SlIndexFormat indexFormat)
        {
            return indexFormat switch
            {
                SlIndexFormat.Uint16 => IndexFormat.Uint16,
                SlIndexFormat.Uint32 => IndexFormat.Uint32,
                _ => throw new ArgumentOutOfRangeException(nameof(indexFormat), indexFormat, null)
            };
        }

        public static LoadOp Convert(this SlLoadOp loadOp)
        {
            return loadOp switch
            {
                SlLoadOp.Clear => LoadOp.Clear,
                SlLoadOp.Load => LoadOp.Load,
                _ => throw new ArgumentOutOfRangeException(nameof(loadOp), loadOp, null)
            };
        }

        public static StoreOp Convert(this SlStoreOp storeOp)
        {
            return storeOp switch
            {
                SlStoreOp.Store => StoreOp.Store,
                SlStoreOp.Discard => StoreOp.Discard,
                _ => throw new ArgumentOutOfRangeException(nameof(storeOp), storeOp, null)
            };
        }

        public static BufferBindingType Convert(this SlBufferBindingType bindingType)
        {
            return bindingType switch
            {
                SlBufferBindingType.Uniform => BufferBindingType.Uniform,
                SlBufferBindingType.Storage => BufferBindingType.Storage,
                SlBufferBindingType.ReadOnlyStorage => BufferBindingType.ReadOnlyStorage,
                _ => throw new ArgumentOutOfRangeException(nameof(bindingType), bindingType, null)
            };
        }

        public static TextureSampleType Convert(this SlTextureSampleType sampleType)
        {
            return sampleType switch
            {
                SlTextureSampleType.Float => TextureSampleType.Float,
                SlTextureSampleType.UnfilterableFloat => TextureSampleType.UnfilterableFloat,
                SlTextureSampleType.Depth => TextureSampleType.Depth,
                SlTextureSampleType.Sint => TextureSampleType.Sint,
                SlTextureSampleType.Uint => TextureSampleType.Uint,
                _ => throw new ArgumentOutOfRangeException(nameof(sampleType), sampleType, null)
            };
        }

        public static SamplerBindingType Convert(this SlSamplerBindingType samplerBindingType)
        {
            return samplerBindingType switch
            {
                SlSamplerBindingType.Filtering => SamplerBindingType.Filtering,
                SlSamplerBindingType.NonFiltering => SamplerBindingType.NonFiltering,
                SlSamplerBindingType.Comparison => SamplerBindingType.Comparison,
                _ => throw new ArgumentOutOfRangeException(nameof(samplerBindingType), samplerBindingType, null)
            };
        }

        public static ShaderStage Convert(this SlShaderStage stage)
        {
            var result = ShaderStage.None;

            if (stage.HasFlag(SlShaderStage.Vertex)) result |= ShaderStage.Vertex;
            if (stage.HasFlag(SlShaderStage.Fragment)) result |= ShaderStage.Fragment;
            if (stage.HasFlag(SlShaderStage.Compute)) result |= ShaderStage.Compute;

            return result;
        }

        public static VertexStepMode Convert(this SlVertexStepMode stepMode)
        {
            return stepMode switch
            {
                SlVertexStepMode.Vertex => VertexStepMode.Vertex,
                SlVertexStepMode.Instance => VertexStepMode.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(stepMode), stepMode, null)
            };
        }

        public static VertexFormat Convert(this SlVertexFormat format)
        {
            return format switch
            {
                SlVertexFormat.Float32 => VertexFormat.Float32,
                SlVertexFormat.Float32x2 => VertexFormat.Float32x2,
                SlVertexFormat.Float32x3 => VertexFormat.Float32x3,
                SlVertexFormat.Float32x4 => VertexFormat.Float32x4,
                SlVertexFormat.Uint8x4 => VertexFormat.Uint8x4,
                SlVertexFormat.Unorm8x4 => VertexFormat.Unorm8x4,
                SlVertexFormat.Sint32 => VertexFormat.Sint32,
                SlVertexFormat.Sint32x2 => VertexFormat.Sint32x2,
                SlVertexFormat.Sint32x3 => VertexFormat.Sint32x3,
                SlVertexFormat.Sint32x4 => VertexFormat.Sint32x4,
                SlVertexFormat.Uint32 => VertexFormat.Uint32,
                SlVertexFormat.Uint32x2 => VertexFormat.Uint32x2,
                SlVertexFormat.Uint32x3 => VertexFormat.Uint32x3,
                SlVertexFormat.Uint32x4 => VertexFormat.Uint32x4,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }

        public static BlendOperation Convert(this SlBlendOperation operation)
        {
            return operation switch
            {
                SlBlendOperation.Add => BlendOperation.Add,
                SlBlendOperation.Subtract => BlendOperation.Subtract,
                SlBlendOperation.ReverseSubtract => BlendOperation.ReverseSubtract,
                SlBlendOperation.Min => BlendOperation.Min,
                SlBlendOperation.Max => BlendOperation.Max,
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
        }

        public static BlendFactor Convert(this SlBlendFactor factor)
        {
            return factor switch
            {
                SlBlendFactor.Zero => BlendFactor.Zero,
                SlBlendFactor.One => BlendFactor.One,
                SlBlendFactor.Src => BlendFactor.Src,
                SlBlendFactor.OneMinusSrc => BlendFactor.OneMinusSrc,
                SlBlendFactor.SrcAlpha => BlendFactor.SrcAlpha,
                SlBlendFactor.OneMinusSrcAlpha => BlendFactor.OneMinusSrcAlpha,
                SlBlendFactor.Dst => BlendFactor.Dst,
                SlBlendFactor.OneMinusDst => BlendFactor.OneMinusDst,
                SlBlendFactor.DstAlpha => BlendFactor.DstAlpha,
                SlBlendFactor.OneMinusDstAlpha => BlendFactor.OneMinusDstAlpha,
                _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null)
            };
        }
    }
}
