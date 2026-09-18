using System.Numerics;
using Solaris.Graph;
using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Renderer.Scene
{
    public interface IScene : IDisposable
    {
        public void Render(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters);
    }
}
