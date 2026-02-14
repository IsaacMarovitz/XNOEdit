using System.Numerics;
using Solaris.RHI;

namespace XNOEdit.Renderer
{
    public abstract class Renderer<TParameters> : IDisposable where TParameters : struct
    {
        protected readonly Shader Shader;

        public Renderer(Shader shader)
        {
            Shader = shader;
        }

        public virtual void Draw(
            SlQueue queue,
            SlRenderPass passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            TParameters parameters)
        {
            BindStaticBindGroups(passEncoder);
        }

        protected void BindStaticBindGroups(SlRenderPass passEncoder)
        {
            for (uint i = 0; i < Shader.BindGroupCount; i++)
            {
                var bindGroup = Shader.GetBindGroup((int)i);

                // Only bind non-null bind groups (some may be dynamic, created elsewhere)
                if (bindGroup != null)
                {
                    passEncoder.SetBindGroup(i, bindGroup);
                }
            }
        }

        public virtual void Dispose()
        {
            Shader.Dispose();
        }
    }
}
