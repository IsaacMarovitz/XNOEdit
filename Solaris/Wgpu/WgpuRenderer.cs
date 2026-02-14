using System.Numerics;
using Silk.NET.WebGPU;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    public abstract unsafe class WgpuRenderer<TParameters> : IDisposable where TParameters : struct
    {
        protected readonly SlDevice Device;
        protected readonly WgpuShader Shader;

        public WgpuRenderer(SlDevice device, WgpuShader shader)
        {
            Device = device;
            Shader = shader;
        }

        public virtual void Draw(
            SlQueue queue,
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
                    // TODO: Clean this up
                    (Device as WgpuDevice).Wgpu.RenderPassEncoderSetBindGroup(passEncoder, i, bindGroup, 0, &dynamicOffset);
                }
            }
        }

        public virtual void Dispose()
        {
            Shader.Dispose();
        }
    }
}
