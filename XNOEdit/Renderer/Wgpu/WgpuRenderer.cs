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
            BindStaticBindGroups(passEncoder);
        }

        protected void BindStaticBindGroups(RenderPassEncoder* passEncoder)
        {
            for (uint i = 0; i < Shader.BindGroupCount; i++)
            {
                var bindGroup = Shader.GetBindGroup((int)i);

                // Only bind non-null bind groups (some may be dynamic, created elsewhere)
                if (bindGroup != null)
                {
                    uint dynamicOffset = 0;
                    Wgpu.RenderPassEncoderSetBindGroup(passEncoder, i, bindGroup, 0, &dynamicOffset);
                }
            }
        }

        public virtual void Dispose()
        {
            Shader?.Dispose();
        }
    }
}
