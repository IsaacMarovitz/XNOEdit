using System.Numerics;
using Solaris.Graph;

namespace XNOEdit.Render
{
    public abstract class Renderer<TParameters> : IDisposable where TParameters : struct
    {
        protected readonly ShaderModule ShaderModule;

        public Renderer(ShaderModule shaderModule)
        {
            ShaderModule = shaderModule;
        }

        public abstract void Draw(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            TParameters parameters);

        public virtual void Dispose()
        {
            ShaderModule.Dispose();
        }
    }
}
