using Silk.NET.WebGPU;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    public unsafe class WgpuQueue : SlQueue
    {
        private WebGPU _wgpu;
        public Queue* Queue { get; }

        internal WgpuQueue(WebGPU wgpu, Queue* queue)
        {
            _wgpu = wgpu;
            Queue = queue;
        }

        public override void Dispose()
        {
            if (Queue != null)
                _wgpu.QueueRelease(Queue);
        }
    }
}
