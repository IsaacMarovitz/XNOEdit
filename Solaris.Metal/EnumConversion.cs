using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal static class EnumConversion
    {
        public static MTLPrimitiveType Convert(this SlPrimitiveTopology topology)
        {
            return topology switch
            {
                SlPrimitiveTopology.PointList => MTLPrimitiveType.Point,
                SlPrimitiveTopology.LineList => MTLPrimitiveType.Line,
                SlPrimitiveTopology.LineStrip => MTLPrimitiveType.LineStrip,
                SlPrimitiveTopology.TriangleList => MTLPrimitiveType.Triangle,
                SlPrimitiveTopology.TriangleStrip => MTLPrimitiveType.TriangleStrip,
                _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
            };
        }

        public static MTLCullMode Convert(this SlCullMode cullMode)
        {
            return cullMode switch
            {
                SlCullMode.None => MTLCullMode.None,
                SlCullMode.Front => MTLCullMode.Front,
                SlCullMode.Back => MTLCullMode.Back,
                _ => throw new ArgumentOutOfRangeException(nameof(cullMode), cullMode, null)
            };
        }

        public static MTLWinding Convert(this SlFrontFace frontFace)
        {
            return frontFace switch
            {
                SlFrontFace.Clockwise => MTLWinding.Clockwise,
                SlFrontFace.CounterClockwise => MTLWinding.CounterClockwise,
                _ => throw new ArgumentOutOfRangeException(nameof(frontFace), frontFace, null)
            };
        }

        public static MTLCompareFunction Convert(this SlCompareFunction compareFunction)
        {
            return compareFunction switch
            {
                SlCompareFunction.Never => MTLCompareFunction.Never,
                SlCompareFunction.Less => MTLCompareFunction.Less,
                SlCompareFunction.LessEqual => MTLCompareFunction.LessEqual,
                SlCompareFunction.Greater => MTLCompareFunction.Greater,
                SlCompareFunction.GreaterEqual => MTLCompareFunction.GreaterEqual,
                SlCompareFunction.Equal => MTLCompareFunction.Equal,
                SlCompareFunction.NotEqual => MTLCompareFunction.NotEqual,
                SlCompareFunction.Always => MTLCompareFunction.Always,
                _ => throw new ArgumentOutOfRangeException(nameof(compareFunction), compareFunction, null)
            };
        }

        public static MTLTextureType Convert(this SlTextureDimension textureDimension)
        {
            return textureDimension switch
            {
                SlTextureDimension.Dimension1D => MTLTextureType.Type1D,
                SlTextureDimension.Dimension2D => MTLTextureType.Type2D,
                SlTextureDimension.Dimension3D => MTLTextureType.Type3D,
                _ => throw new ArgumentOutOfRangeException(nameof(textureDimension), textureDimension, null)
            };
        }

        public static MTLTextureType Convert(SlTextureViewDimension textureViewDimension)
        {
            return textureViewDimension switch
            {
                SlTextureViewDimension.Dimension1D => MTLTextureType.Type1D,
                SlTextureViewDimension.Dimension2D => MTLTextureType.Type2D,
                SlTextureViewDimension.Dimension2DArray => MTLTextureType.Type2DArray,
                SlTextureViewDimension.DimensionCube => MTLTextureType.Cube,
                SlTextureViewDimension.DimensionCubeArray => MTLTextureType.Type2DArray,
                SlTextureViewDimension.Dimension3D => MTLTextureType.Type3D,
                _ => throw new ArgumentOutOfRangeException(nameof(textureViewDimension), textureViewDimension, null)
            };
        }

        public static MTLTextureUsage Convert(this SlTextureUsage usage)
        {
            var result = MTLTextureUsage.Unknown;

            if (usage.HasFlag(SlTextureUsage.TextureBinding)) result |= MTLTextureUsage.ShaderRead;
            if (usage.HasFlag(SlTextureUsage.StorageBinding)) result |= MTLTextureUsage.ShaderRead | MTLTextureUsage.ShaderWrite;
            if (usage.HasFlag(SlTextureUsage.RenderAttachment)) result |= MTLTextureUsage.RenderTarget;

            return result;
        }

        public static MTLPixelFormat Convert(this SlTextureFormat format)
        {
            return format switch
            {
                SlTextureFormat.R8Unorm => MTLPixelFormat.R8Unorm,
                SlTextureFormat.R8Snorm => MTLPixelFormat.R8Snorm,
                SlTextureFormat.R8Uint => MTLPixelFormat.R8Uint,
                SlTextureFormat.R8Sint => MTLPixelFormat.R8Sint,
                SlTextureFormat.R16Uint => MTLPixelFormat.R16Uint,
                SlTextureFormat.R16Sint => MTLPixelFormat.R16Sint,
                SlTextureFormat.R16float => MTLPixelFormat.R16Float,
                SlTextureFormat.RG8Unorm => MTLPixelFormat.RG8Unorm,
                SlTextureFormat.RG8Snorm => MTLPixelFormat.RG8Snorm,
                SlTextureFormat.RG8Uint => MTLPixelFormat.RG8Uint,
                SlTextureFormat.RG8Sint => MTLPixelFormat.RG8Sint,
                SlTextureFormat.R32float => MTLPixelFormat.R32Float,
                SlTextureFormat.R32Uint => MTLPixelFormat.R32Uint,
                SlTextureFormat.R32Sint => MTLPixelFormat.R32Sint,
                SlTextureFormat.RG16Uint => MTLPixelFormat.RG16Uint,
                SlTextureFormat.RG16Sint => MTLPixelFormat.RG16Sint,
                SlTextureFormat.RG16float => MTLPixelFormat.RG16Float,
                SlTextureFormat.Rgba8Unorm => MTLPixelFormat.RGBA8Unorm,
                SlTextureFormat.Rgba8UnormSrgb => MTLPixelFormat.RGBA8UnormsRGB,
                SlTextureFormat.Rgba8Snorm => MTLPixelFormat.RGBA8Snorm,
                SlTextureFormat.Rgba8Uint => MTLPixelFormat.RGBA8Uint,
                SlTextureFormat.Rgba8Sint => MTLPixelFormat.RGBA8Sint,
                SlTextureFormat.Bgra8Unorm => MTLPixelFormat.BGRA8Unorm,
                SlTextureFormat.Bgra8UnormSrgb => MTLPixelFormat.BGRA8UnormsRGB,
                SlTextureFormat.Rgba16Uint => MTLPixelFormat.RGBA16Uint,
                SlTextureFormat.Rgba16Sint => MTLPixelFormat.RGBA16Sint,
                SlTextureFormat.Rgba16float => MTLPixelFormat.RGBA16Float,
                SlTextureFormat.Rgba32float => MTLPixelFormat.RGBA32Float,
                SlTextureFormat.Rgba32Uint => MTLPixelFormat.RGBA32Uint,
                SlTextureFormat.Rgba32Sint => MTLPixelFormat.RGBA32Sint,
                SlTextureFormat.Stencil8 => MTLPixelFormat.Stencil8,
                SlTextureFormat.Depth16Unorm => MTLPixelFormat.Depth16Unorm,
                SlTextureFormat.Depth24PlusStencil8 => MTLPixelFormat.Depth24UnormStencil8,
                SlTextureFormat.Depth32float => MTLPixelFormat.Depth32Float,
                SlTextureFormat.Depth32floatStencil8 => MTLPixelFormat.Depth32FloatStencil8,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }

        public static MTLSamplerAddressMode Convert(this SlAddressMode addressMode)
        {
            return addressMode switch
            {
                SlAddressMode.Repeat => MTLSamplerAddressMode.Repeat,
                SlAddressMode.MirrorRepeat => MTLSamplerAddressMode.MirrorRepeat,
                SlAddressMode.ClampToEdge => MTLSamplerAddressMode.ClampToEdge,
                _ => throw new ArgumentOutOfRangeException(nameof(addressMode), addressMode, null)
            };
        }

        public static MTLSamplerMinMagFilter Convert(this SlFilterMode filterMode)
        {
            return filterMode switch
            {
                SlFilterMode.Nearest => MTLSamplerMinMagFilter.Nearest,
                SlFilterMode.Linear => MTLSamplerMinMagFilter.Linear,
                _ => throw new ArgumentOutOfRangeException(nameof(filterMode), filterMode, null)
            };
        }

        public static MTLSamplerMipFilter MipmapConvert(this SlFilterMode filterMode)
        {
            return filterMode switch
            {
                SlFilterMode.Nearest => MTLSamplerMipFilter.Nearest,
                SlFilterMode.Linear => MTLSamplerMipFilter.Linear,
                _ => throw new ArgumentOutOfRangeException(nameof(filterMode), filterMode, null)
            };
        }

        public static MTLIndexType Convert(this SlIndexFormat indexFormat)
        {
            return indexFormat switch
            {
                SlIndexFormat.Uint16 => MTLIndexType.UInt16,
                SlIndexFormat.Uint32 => MTLIndexType.UInt32,
                _ => throw new ArgumentOutOfRangeException(nameof(indexFormat), indexFormat, null)
            };
        }

        public static MTLLoadAction Convert(this SlLoadOp loadOp)
        {
            return loadOp switch
            {
                SlLoadOp.Clear => MTLLoadAction.Clear,
                SlLoadOp.Load => MTLLoadAction.Load,
                _ => throw new ArgumentOutOfRangeException(nameof(loadOp), loadOp, null)
            };
        }

        public static MTLStoreAction Convert(this SlStoreOp storeOp)
        {
            return storeOp switch
            {
                SlStoreOp.Store => MTLStoreAction.Store,
                SlStoreOp.Discard => MTLStoreAction.DontCare,
                _ => throw new ArgumentOutOfRangeException(nameof(storeOp), storeOp, null)
            };
        }

        public static MTLStages Convert(this SlShaderStage stage)
        {
            MTLStages result = 0;

            if (stage.HasFlag(SlShaderStage.Vertex)) result |= MTLStages.StageVertex;
            if (stage.HasFlag(SlShaderStage.Fragment)) result |= MTLStages.StageFragment;
            if (stage.HasFlag(SlShaderStage.Compute)) result |= MTLStages.StageDispatch;

            return result;
        }

        public static MTLVertexStepFunction Convert(this SlVertexStepMode stepMode)
        {
            return stepMode switch
            {
                SlVertexStepMode.Vertex => MTLVertexStepFunction.PerVertex,
                SlVertexStepMode.Instance => MTLVertexStepFunction.PerInstance,
                _ => throw new ArgumentOutOfRangeException(nameof(stepMode), stepMode, null)
            };
        }

        public static MTLVertexFormat Convert(this SlVertexFormat format)
        {
            return format switch
            {
                SlVertexFormat.Float32 => MTLVertexFormat.Float,
                SlVertexFormat.Float32x2 => MTLVertexFormat.Float2,
                SlVertexFormat.Float32x3 => MTLVertexFormat.Float3,
                SlVertexFormat.Float32x4 => MTLVertexFormat.Float4,
                SlVertexFormat.Uint8x4 => MTLVertexFormat.UChar4,
                SlVertexFormat.Unorm8x4 => MTLVertexFormat.UChar4Normalized,
                SlVertexFormat.Sint32 => MTLVertexFormat.Int,
                SlVertexFormat.Sint32x2 => MTLVertexFormat.Int2,
                SlVertexFormat.Sint32x3 => MTLVertexFormat.Int3,
                SlVertexFormat.Sint32x4 => MTLVertexFormat.Int4,
                SlVertexFormat.Uint32 => MTLVertexFormat.UInt,
                SlVertexFormat.Uint32x2 => MTLVertexFormat.UInt2,
                SlVertexFormat.Uint32x3 => MTLVertexFormat.UInt3,
                SlVertexFormat.Uint32x4 => MTLVertexFormat.UInt4,
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
            };
        }

        public static MTLBlendOperation Convert(this SlBlendOperation operation)
        {
            return operation switch
            {
                SlBlendOperation.Add => MTLBlendOperation.Add,
                SlBlendOperation.Subtract => MTLBlendOperation.Subtract,
                SlBlendOperation.ReverseSubtract => MTLBlendOperation.ReverseSubtract,
                SlBlendOperation.Min => MTLBlendOperation.Min,
                SlBlendOperation.Max => MTLBlendOperation.Max,
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
        }

        public static MTLBlendFactor Convert(this SlBlendFactor factor)
        {
            return factor switch
            {
                SlBlendFactor.Zero => MTLBlendFactor.Zero,
                SlBlendFactor.One => MTLBlendFactor.One,
                SlBlendFactor.Src => MTLBlendFactor.SourceColor,
                SlBlendFactor.OneMinusSrc => MTLBlendFactor.OneMinusSourceColor,
                SlBlendFactor.SrcAlpha => MTLBlendFactor.SourceAlpha,
                SlBlendFactor.OneMinusSrcAlpha => MTLBlendFactor.OneMinusSourceAlpha,
                SlBlendFactor.Dst => MTLBlendFactor.DestinationColor,
                SlBlendFactor.OneMinusDst => MTLBlendFactor.OneMinusDestinationColor,
                SlBlendFactor.DstAlpha => MTLBlendFactor.DestinationAlpha,
                SlBlendFactor.OneMinusDstAlpha => MTLBlendFactor.OneMinusDestinationAlpha,
                _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null)
            };
        }
    }
}
