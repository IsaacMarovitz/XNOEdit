using Silk.NET.WebGPU;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    public unsafe class WgpuTextureView : SlTextureView
    {
        private readonly WebGPU _wgpu;
        public TextureView* TextureView { get; }

        internal WgpuTextureView(WebGPU wgpu, TextureView* textureView)
        {
            _wgpu = wgpu;
            TextureView = textureView;
        }

        public override void* GetHandle()
        {
            return TextureView;
        }

        public override void Dispose()
        {
            if (TextureView != null)
                _wgpu.TextureViewRelease(TextureView);
        }
    }
}
