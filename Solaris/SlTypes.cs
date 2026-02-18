namespace Solaris
{
    [Flags]
    public enum SlBufferUsage
    {
        None     = 0,
        MapRead  = 1 << 0,
        MapWrite = 1 << 1,
        CopySrc  = 1 << 2,
        CopyDst  = 1 << 3,
        Index    = 1 << 4,
        Vertex   = 1 << 5,
        Uniform  = 1 << 6,
        Storage  = 1 << 7,
    }

    public struct SlBufferDescriptor
    {
        public SlBufferUsage Usage;
        public ulong Size;
        public int Alignment;
    }

    public enum SlPrimitiveTopology
    {
        PointList,
        LineList,
        LineStrip,
        TriangleList,
        TriangleStrip,
    }

    public enum SlCullMode
    {
        None,
        Front,
        Back,
    }

    public enum SlFrontFace
    {
        Clockwise,
        CounterClockwise,
    }

    public enum SlCompareFunction
    {
        Never,
        Less,
        LessEqual,
        Greater,
        GreaterEqual,
        Equal,
        NotEqual,
        Always,
    }

    public enum SlTextureDimension
    {
        Dimension1D,
        Dimension2D,
        Dimension3D,
    }

    public enum SlTextureViewDimension
    {
        DimensionUndefined,
        Dimension1D,
        Dimension2D,
        Dimension2DArray,
        DimensionCube,
        DimensionCubeArray,
        Dimension3D,
    }

    [Flags]
    public enum SlTextureUsage
    {
        None             = 0,
        CopySrc          = 1 << 0,
        CopyDst          = 1 << 1,
        TextureBinding   = 1 << 2,
        StorageBinding   = 1 << 3,
        RenderAttachment = 1 << 4,
    }

    public enum SlTextureFormat
    {
        R8Unorm,
        R8Snorm,
        R8Uint,
        R8Sint,

        R16Uint,
        R16Sint,
        R16float,

        RG8Unorm,
        RG8Snorm,
        RG8Uint,
        RG8Sint,

        R32float,
        R32Uint,
        R32Sint,

        RG16Uint,
        RG16Sint,
        RG16float,

        Rgba8Unorm,
        Rgba8UnormSrgb,
        Rgba8Snorm,
        Rgba8Uint,
        Rgba8Sint,
        Bgra8Unorm,
        Bgra8UnormSrgb,

        Rgba16Uint,
        Rgba16Sint,
        Rgba16float,

        Rgba32float,
        Rgba32Uint,
        Rgba32Sint,

        Stencil8,
        Depth16Unorm,
        Depth24Plus,
        Depth24PlusStencil8,
        Depth32float,
        Depth32floatStencil8
    }

    public struct SlExtent3D
    {
        public uint Width;
        public uint Height;
        public uint DepthOrArrayLayers;
    }

    public struct SlTextureDescriptor
    {
        public SlExtent3D Size;
        public uint MipLevelCount;
        public uint SampleCount;
        public SlTextureDimension Dimension;
        public SlTextureFormat Format;
        public SlTextureUsage Usage;
    }

    public struct SlTextureViewDescriptor
    {
        public SlTextureFormat Format;
        public SlTextureViewDimension Dimension;
        public uint BaseMipLevel;
        public uint MipLevelCount;
        public uint BaseArrayLayer;
        public uint ArrayLayerCount;
    }

    public enum SlAddressMode
    {
        Repeat,
        MirrorRepeat,
        ClampToEdge,
    }

    public enum SlFilterMode
    {
        Nearest,
        Linear,
    }

    public struct SlSamplerDescriptor
    {
        public SlAddressMode AddressModeU;
        public SlAddressMode AddressModeV;
        public SlAddressMode AddressModeW;
        public SlFilterMode MagFilter;
        public SlFilterMode MinFilter;
        public SlFilterMode MipmapFilter;
        public float LodMinClamp;
        public float LodMaxClamp;
        public ushort MaxAnisotropy;
    }

    public struct SlOrigin3D
    {
        public uint X;
        public uint Y;
        public uint Z;
    }

    public struct SlCopyTextureDescriptor
    {
        public SlTexture Texture;
        public uint MipLevel;
        public SlOrigin3D Origin;
    }

    public struct SlTextureDataLayout
    {
        public ulong Offset;
        public uint BytesPerRow;
        public uint RowsPerImage;
    }

    public struct SlSurfaceDescriptor
    {
        public uint Width;
        public uint Height;
        public SlTextureFormat Format;
        public SlTextureUsage Usage;
        public SlPresentMode PresentMode;
    }

    public enum SlPresentMode
    {
        Fifo,
        Immediate,
        Mailbox,
    }

    public enum SlIndexFormat
    {
        Uint16,
        Uint32,
    }

    public struct SlRenderPassDescriptor
    {
        public SlColorAttachment[] ColorAttachments;
        public SlDepthStencilAttachment? DepthStencilAttachment;
    }

    public struct SlColorAttachment
    {
        public SlTextureView View;
        public SlLoadOp LoadOp;
        public SlStoreOp StoreOp;
        public SlColor ClearValue;
    }

    public struct SlDepthStencilAttachment
    {
        public SlTextureView View;
        public SlLoadOp DepthLoadOp;
        public SlStoreOp DepthStoreOp;
        public float DepthClearValue;
    }

    public enum SlLoadOp
    {
        Clear,
        Load,
    }

    public enum SlStoreOp
    {
        Store,
        Discard,
    }

    public struct SlColor
    {
        public double R, G, B, A;

        public SlColor(double r, double g, double b, double a = 1.0)
        {
            R = r; G = g; B = b; A = a;
        }
    }

    public struct SlBindGroupLayoutDescriptor
    {
        public SlBindGroupLayoutEntry[] Entries;
    }

    public struct SlBindGroupLayoutEntry
    {
        public uint Binding;
        public SlShaderStage Visibility;
        public SlBindingType Type;

        // Only relevant for buffer bindings
        public SlBufferBindingType BufferType;

        // Only relevant for texture bindings
        public SlTextureViewDimension TextureDimension;
        public SlTextureSampleType TextureSampleType;

        // Only relevant for sampler bindings
        public SlSamplerBindingType SamplerType;
    }

    public enum SlBindingType
    {
        Invalid,
        Buffer,
        Texture,
        Sampler,
    }

    public enum SlBufferBindingType
    {
        Uniform,
        Storage,
        ReadOnlyStorage,
    }

    public enum SlTextureSampleType
    {
        Float,
        UnfilterableFloat,
        Depth,
        Sint,
        Uint,
    }

    public enum SlSamplerBindingType
    {
        Filtering,
        NonFiltering,
        Comparison,
    }

    [Flags]
    public enum SlShaderStage
    {
        None     = 0,
        Vertex   = 1 << 0,
        Fragment = 1 << 1,
        Compute  = 1 << 2,
    }

    public struct SlBindGroupDescriptor
    {
        public SlBindGroupLayout Layout;
        public SlBindGroupEntry[] Entries;
    }

    public struct SlBindGroupEntry
    {
        public uint Binding;

        // Set exactly one of these — the rest should be null/default
        public SlBufferBinding? Buffer;
        public SlTextureView? TextureView;
        public SlSampler? Sampler;
    }

    public struct SlBufferBinding
    {
        /// <summary>
        /// CPU-side native handle (used by WebGPU backend).
        /// </summary>
        public unsafe void* Handle;

        /// <summary>
        /// GPU virtual address (used by Metal 4 bindless backend).
        /// 0 on backends that don't support GPU addresses.
        /// </summary>
        public ulong GpuAddress;

        public ulong Offset;
        public ulong Size;

        public ISlBuffer Source;

        /// <summary>
        /// Create a buffer binding from a buffer, automatically populating
        /// handle, GPU address, and size.
        /// </summary>
        public static unsafe SlBufferBinding From<T>(SlBuffer<T> buffer, ulong offset = 0, ulong? size = null) where T : unmanaged
        {
            return new SlBufferBinding
            {
                Handle = buffer.GetHandle(),
                GpuAddress = buffer.GpuAddress,
                Offset = offset,
                Size = size ?? buffer.Size,
                Source = buffer
            };
        }
    }

    public struct SlShaderModuleDescriptor
    {
        public string Label;

        /// <summary>
        /// Shader source — interpretation is backend-specific.
        /// WGSL string for WebGPU, SPIR-V bytes for Vulkan, MSL for Metal, HLSL/DXIL for D3D.
        /// </summary>
        public object Source;

        /// <summary>
        /// Hint for which language the source is in.
        /// </summary>
        public SlShaderLanguage Language;
    }

    public enum SlShaderLanguage
    {
        Wgsl,
        SpirV,
        Msl,
        Hlsl,
    }

    public struct SlRenderPipelineDescriptor
    {
        public SlShaderModule Shader;
        public string VertexEntryPoint;
        public string FragmentEntryPoint;
        public SlVertexBufferLayout[] VertexBufferLayouts;
        public SlBindGroupLayout[] BindGroupLayouts;
        public SlPipelineVariantDescriptor Variant;
        public SlTextureFormat ColorFormat;
        public SlTextureFormat? DepthFormat;
        public SlBlendState? BlendState;
    }

    public struct SlVertexBufferLayout
    {
        public ulong Stride;
        public SlVertexStepMode StepMode;
        public SlVertexAttribute[] Attributes;
    }

    public enum SlVertexStepMode
    {
        Vertex,
        Instance,
    }

    public struct SlVertexAttribute
    {
        public SlVertexFormat Format;
        public ulong Offset;
        public uint ShaderLocation;
    }

    public enum SlVertexFormat
    {
        Float32,
        Float32x2,
        Float32x3,
        Float32x4,
        Uint8x4,
        Unorm8x4,
        Sint32,
        Sint32x2,
        Sint32x3,
        Sint32x4,
        Uint32,
        Uint32x2,
        Uint32x3,
        Uint32x4,
    }

    public struct SlBlendState
    {
        public SlBlendComponent Color;
        public SlBlendComponent Alpha;
    }

    public struct SlBlendComponent
    {
        public SlBlendOperation Operation;
        public SlBlendFactor SrcFactor;
        public SlBlendFactor DstFactor;
    }

    public enum SlBlendOperation
    {
        Add,
        Subtract,
        ReverseSubtract,
        Min,
        Max,
    }

    public enum SlBlendFactor
    {
        Zero,
        One,
        Src,
        OneMinusSrc,
        SrcAlpha,
        OneMinusSrcAlpha,
        Dst,
        OneMinusDst,
        DstAlpha,
        OneMinusDstAlpha,
    }
}
