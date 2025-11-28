using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Renderer.Scene
{
    public interface IScene : IDisposable
    {
        public unsafe void Render(Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters);
    }
}
