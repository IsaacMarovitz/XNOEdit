using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    public class PipelineVariantDescriptor
    {
        public PrimitiveTopology Topology;
        public CullMode CullMode;
        public FrontFace FrontFace;
        public bool DepthWrite;
        public CompareFunction DepthCompare;
        public bool AlphaBlend;
    }
}
