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
    }
}
