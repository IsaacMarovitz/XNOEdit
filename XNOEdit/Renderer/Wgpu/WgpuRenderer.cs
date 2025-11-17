using System.Numerics;
using Silk.NET.WebGPU;

namespace XNOEdit.Renderer.Wgpu
{
    public abstract unsafe class WgpuRenderer<TParameters> : IDisposable where TParameters : struct
    {
        protected readonly WebGPU Wgpu;
        protected readonly WgpuShader Shader;

        public WgpuRenderer(WebGPU wgpu, WgpuShader shader)
        {
            Wgpu = wgpu;
            Shader = shader;
        }

        public virtual void Draw(
            Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            TParameters parameters)
        {
            uint dynamicOffset = 0;
            Wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, Shader.BindGroup, 0, &dynamicOffset);
        }

        public virtual void Dispose()
        {
            Wgpu?.Dispose();
            Shader?.Dispose();
        }
    }
}
