using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuSampler : SlSampler
    {
        private readonly WebGPU _wgpu;
        public Sampler* Sampler { get; }

        internal WgpuSampler(WebGPU wgpu, Sampler* sampler)
        {
            _wgpu = wgpu;
            Sampler = sampler;
        }

        public override void* GetHandle()
        {
            return Sampler;
        }

        public override void Dispose()
        {
            if (Sampler != null)
            {
                _wgpu.SamplerRelease(Sampler);
            }
        }
    }
}
