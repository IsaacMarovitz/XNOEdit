using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Solaris;
using Solaris.Graph;
using XNOEdit.Guest;
using XNOEdit.Logging;
using XNOEdit.Managers;

namespace XNOEdit.Renderer.Renderers
{
    public struct ModelParameters
    {
        public Vector3 SunDirection;
        public Vector3 SunColor;
        public Vector3 Position;
        public bool CullBackfaces;
        public GuestDrawPhase GuestPhase;
        public GuestDrawContext GuestDraw;
        public TextureManager TextureManager;

        public readonly GuestSceneState ToSceneState(Matrix4x4 view, Matrix4x4 projection) => new()
        {
            View = view,
            Projection = projection,
            CameraPosition = Position,
            SunDirection = SunDirection,
            SunColor = SunColor,
            Ambient = SunColor * 0.3f,
            CullBackfaces = CullBackfaces,
        };
    }

    public class ModelRenderer : IDisposable
    {
        private readonly Model _model;
        private readonly GuestDrawContext _guestDraw = new();
        private bool _reportedGuestFallback;

        private Matrix4x4[] _instances = [Matrix4x4.Identity];

        public ModelRenderer(
            SlDevice device,
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk,
            GuestMaterialCache? guestMaterial)
        {
            _model = new Model(device, objectChunk, textureListChunk, effectListChunk, guestMaterial);
        }

        public int InstanceCount => _instances.Length;

        public void SetInstances(Matrix4x4[] instances) => _instances = instances;

        public bool GetVisible() => _model.GetAnyMeshVisible();
        public void SetVisible(bool visible) => _model.SetAllVisible(visible);
        public bool GetSubobjectVisible(int subobject) => _model.GetSubobjectVisible(subobject);
        public bool GetMeshSetVisible(int subobject, int meshSet) => _model.GetMeshSetVisible(subobject, meshSet);

        public void SetVisible(int subobject, int? meshSet, bool visibility)
        {
            _model.SetVisible(subobject, meshSet, visibility);
        }

        public void Draw(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters)
        {
            if (_instances.Length == 0)
                return;

            var scene = modelParameters.ToSceneState(view, projection);

            var skipped = _model.DrawGuest(
                ctx, modelParameters.GuestDraw, modelParameters.TextureManager,
                in scene, _instances, modelParameters.GuestPhase);

            if (skipped > 0 && !_reportedGuestFallback && modelParameters.GuestPhase == GuestDrawPhase.Opaque)
            {
                _reportedGuestFallback = true;
                Logger.Warning?.PrintMsg(LogClass.Application,
                    $"{skipped} mesh(es) have no recompiled shader and were not drawn");
            }
        }

        public void CollectTransparent(List<GuestTransparentDraw> sink, Matrix4x4 view)
        {
            _model.CollectTransparent(sink, view, _instances);
        }

        public void Dispose()
        {
            _model.Dispose();
        }
    }
}
