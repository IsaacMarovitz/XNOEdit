using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuCommandBuffer : SlCommandBuffer
    {
        private readonly WebGPU _wgpu;
        private readonly CommandBuffer* _handle;

        public static implicit operator CommandBuffer*(WgpuCommandBuffer buffer) => buffer._handle;

        internal WgpuCommandBuffer(WebGPU wgpu, CommandBuffer* handle)
        {
            _wgpu = wgpu;
            _handle = handle;
        }

        public override void Dispose()
        {
            if (_handle != null)
                _wgpu.CommandBufferRelease(_handle);
        }
    }
}
