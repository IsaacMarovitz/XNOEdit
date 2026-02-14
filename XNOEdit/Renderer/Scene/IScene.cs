using System.Numerics;
using Solaris;
using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Renderer.Scene
{
    public interface IScene : IDisposable
    {
        public void Render(
            SlQueue queue,
            SlRenderPass passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters);
    }
}
