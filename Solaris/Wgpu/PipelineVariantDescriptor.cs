using Silk.NET.WebGPU;

namespace XNOEdit.Renderer.Wgpu
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
