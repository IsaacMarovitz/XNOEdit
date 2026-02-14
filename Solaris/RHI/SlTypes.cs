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
}
