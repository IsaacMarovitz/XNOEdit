using Silk.NET.WebGPU;
using Solaris.RHI;

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
    }
}
