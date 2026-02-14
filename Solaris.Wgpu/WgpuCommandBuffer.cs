using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuCommandBuffer : SlCommandBuffer
    {
        private readonly WebGPU _wgpu;
        public CommandBuffer* CommandBuffer { get; }

        internal WgpuCommandBuffer(WebGPU wgpu, CommandBuffer* commandBuffer)
        {
            _wgpu = wgpu;
            CommandBuffer = commandBuffer;
        }

        public override void Dispose()
        {
            if (CommandBuffer != null)
                _wgpu.CommandBufferRelease(CommandBuffer);
        }
    }
}
