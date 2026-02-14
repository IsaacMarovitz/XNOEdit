using System.Numerics;
using Silk.NET.WebGPU;
using Solaris.RHI;
using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Renderer.Scene
{
    public interface IScene : IDisposable
    {
        public unsafe void Render(
            SlQueue queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters);
    }
}
