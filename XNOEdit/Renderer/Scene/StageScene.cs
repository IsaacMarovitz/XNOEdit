using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Renderer.Scene
{
    public class StageScene : IScene
    {
        private readonly ModelRenderer[] _renderers;

        public StageScene(ModelRenderer[] renderers)
        {
            _renderers = renderers;
        }

        public void SetVisible(int xnoIndex, bool visibility)
        {
            _renderers[xnoIndex]?.Visible = visibility;
        }

        public void SetObjectVisible(int xnoIndex, int objectIndex, int? meshIndex, bool visibility)
        {
            _renderers[xnoIndex]?.SetVisible(objectIndex, meshIndex, visibility);
        }

        public unsafe void Render(Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters)
        {
            foreach (var renderer in _renderers)
            {
                renderer?.Draw(queue, passEncoder, view, projection, modelParameters);
            }
        }

        public void Dispose()
        {
            foreach (var renderer in _renderers)
            {
                renderer?.Dispose();
            }
        }
    }
}
