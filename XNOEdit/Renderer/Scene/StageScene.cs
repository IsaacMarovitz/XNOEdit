using System.Numerics;
using Solaris;
using Solaris.Graph;
using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Renderer.Scene
{
    public class StageScene : IScene
    {
        private readonly ModelRenderer[] _renderers;
        private readonly Dictionary<string, InstancedModelRenderer> _instancedRenderers = [];
        private SlDevice _device;

        public string? TerrainName { get; }

        public StageScene(SlDevice device, ModelRenderer[] renderers, string? terrainName = null)
        {
            _device = device;
            _renderers = renderers;
            TerrainName = terrainName;
        }

        public void AddInstancedRenderer(string name, InstancedModelRenderer renderer)
        {
            if (_instancedRenderers.TryGetValue(name, out var existing))
                _device.Retire(existing);

            _instancedRenderers[name] = renderer;
        }

        public void RemoveInstancedRenderer(string name)
        {
            if (_instancedRenderers.TryGetValue(name, out var renderer))
            {
                renderer.Dispose();
                _instancedRenderers.Remove(name);
            }
        }

        public void ClearInstancedRenderers()
        {
            foreach (var renderer in _instancedRenderers.Values)
                renderer.Dispose();

            _instancedRenderers.Clear();
        }

        public void SetVisible(int xnoIndex, bool visibility)
        {
            _renderers[xnoIndex].SetVisible(visibility);
        }

        public void SetObjectVisible(int xnoIndex, int objectIndex, int? meshIndex, bool visibility)
        {
            _renderers[xnoIndex].SetVisible(objectIndex, meshIndex, visibility);
        }

        public void Render(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters)
        {
            foreach (var renderer in _renderers)
            {
                renderer.Draw(ctx, view, projection, modelParameters);
            }

            var instancedParams = new InstancedModelParameters
            {
                SunDirection = modelParameters.SunDirection,
                SunColor = modelParameters.SunColor,
                CameraPosition = modelParameters.Position,
                VertColorStrength = modelParameters.VertColorStrength,
                Wireframe = modelParameters.Wireframe,
                CullBackfaces = modelParameters.CullBackfaces,
                Lightmap = modelParameters.Lightmap,
                TextureManager = modelParameters.TextureManager
            };

            foreach (var renderer in _instancedRenderers.Values)
            {
                renderer.Draw(ctx, view, projection, instancedParams);
            }
        }

        public void Dispose()
        {
            foreach (var renderer in _renderers)
                renderer.Dispose();

            foreach (var renderer in _instancedRenderers.Values)
                renderer.Dispose();

            _instancedRenderers.Clear();
        }
    }
}
