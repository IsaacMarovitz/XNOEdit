namespace Solaris.RHI
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
}
