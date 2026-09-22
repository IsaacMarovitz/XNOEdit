using System.Numerics;
using Solaris;
using Solaris.Graph;
using XNOEdit.Guest;
using XNOEdit.Render.Renderers;

namespace XNOEdit.Render
{
    public sealed class Scene : IDisposable
    {
        private readonly SlDevice _device;
        private readonly ModelRenderer[] _renderers;
        private readonly Dictionary<string, ModelRenderer> _placed = [];
        private readonly List<GuestTransparentDraw> _transparentDraws = [];
        private readonly SlTextureIndex _envMap;

        public Scene(SlDevice device, ModelRenderer[] renderers, SlTextureIndex envMap, string? terrainName = null)
        {
            _device = device;
            _renderers = renderers;
            _envMap = envMap;
            TerrainName = terrainName;
        }

        public string? TerrainName { get; }

        public void AddPlaced(string name, ModelRenderer renderer)
        {
            if (_placed.TryGetValue(name, out var existing))
                _device.Retire(existing);

            _placed[name] = renderer;
        }

        public void RemovePlaced(string name)
        {
            if (_placed.Remove(name, out var renderer))
                renderer.Dispose();
        }

        public void ClearPlaced()
        {
            foreach (var renderer in _placed.Values)
                renderer.Dispose();

            _placed.Clear();
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
            foreach (var phase in GuestDrawPhases.Ordered)
            {
                modelParameters.GuestPhase = phase;
                modelParameters.EnvMap = _envMap;

                if (phase == GuestDrawPhase.Transparent)
                {
                    DrawTransparent(ctx, view, projection, in modelParameters);
                    continue;
                }

                // Collapsing the depth range puts the sky behind everything; its
                // pipeline compares GreaterEqual so it fills only untouched pixels.
                if (phase == GuestDrawPhase.Sky)
                    ctx.SetViewportDepthRange(0.0f, 0.0f);

                foreach (var renderer in _renderers)
                    renderer.Draw(ctx, view, projection, modelParameters);

                foreach (var renderer in _placed.Values)
                    renderer.Draw(ctx, view, projection, modelParameters);

                if (phase == GuestDrawPhase.Sky)
                    ctx.SetViewportDepthRange(0.0f, 1.0f);
            }
        }

        private void DrawTransparent(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            in ModelParameters modelParameters)
        {
            _transparentDraws.Clear();

            foreach (var renderer in _renderers)
                renderer.CollectTransparent(_transparentDraws, view);

            foreach (var renderer in _placed.Values)
                renderer.CollectTransparent(_transparentDraws, view);

            if (_transparentDraws.Count == 0)
                return;

            // Ascending view-space Z is far to near under a right-handed view.
            _transparentDraws.Sort((a, b) => a.ViewDepth.CompareTo(b.ViewDepth));

            var scene = modelParameters.ToSceneState(view, projection);

            foreach (var draw in _transparentDraws)
            {
                draw.Mesh.DrawGuestInstance(
                    ctx, modelParameters.GuestDraw, modelParameters.TextureManager, in scene, draw.World);
            }
        }

        public void Dispose()
        {
            foreach (var renderer in _renderers)
                renderer.Dispose();

            ClearPlaced();
        }
    }
}
