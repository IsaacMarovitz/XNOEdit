using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Renderer.Scene
{
    public class ObjectScene : IScene
    {
        private readonly ModelRenderer _renderer;

        public ObjectScene(ModelRenderer renderer)
        {
            _renderer = renderer;
        }

        public void SetVisible(int objectIndex, int? meshIndex, bool visibility)
        {
            _renderer?.SetVisible(objectIndex, meshIndex, visibility);
        }

        public unsafe void Render(Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters)
        {
            _renderer?.Draw(queue, passEncoder, view, projection, modelParameters);
        }

        public void Dispose()
        {
            _renderer?.Dispose();
        }
    }
}
