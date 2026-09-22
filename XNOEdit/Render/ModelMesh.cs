using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using Solaris;
using Solaris.Graph;
using XNOEdit.Guest;
using XNOEdit.Managers;

namespace XNOEdit.Render
{
    public class ModelMesh : IDisposable
    {
        /// <summary>The game binds material textures to s0-s3.</summary>
        private const int StageCount = 4;

        public int Subobject { get; }
        public int MeshSet { get; }
        public bool Visible { get; private set; } = true;
        public GuestDrawBucket Bucket { get; }
        public Vector3 Centre { get; }

        private readonly MeshGeometry _geometry;
        private readonly GuestMeshState _state;

        private readonly Vector4 _diffuse;
        private readonly Vector4 _ambient;
        private readonly Vector4 _specular;
        private readonly Vector4 _emission;
        private readonly float _power;

        private readonly Vector2[] _stageOffsets = new Vector2[StageCount];
        private readonly string?[] _stageTextures = new string?[StageCount];

        public GuestMaterial? GuestMaterial { get; }

        public ModelMesh(
            SlDevice device,
            SlBuffer sharedVbo,
            PrimitiveList primitiveList,
            TextureListChunk? textureList,
            Material material,
            GuestMaterial? guestMaterial,
            GuestDrawBucket bucket,
            Vector3 centre,
            int subobject,
            int meshSet)
        {
            GuestMaterial = guestMaterial;
            Subobject = subobject;
            MeshSet = meshSet;
            Bucket = bucket;
            Centre = centre;

            _geometry = MeshGeometry.CreateFromTriangleStrip(
                device, sharedVbo, primitiveList.StripIndices, primitiveList.IndexIndices);

            BuildStageTextures(material, textureList);

            _state = GuestRenderState.FromLogic(material.Logic);
            _diffuse = PropertyUtility.MaterialColorToVec4(material.Colour.Diffuse);
            _ambient = PropertyUtility.MaterialColorToVec4(material.Colour.Ambient);
            _specular = PropertyUtility.MaterialColorToVec4(material.Colour.Specular);
            _emission = PropertyUtility.MaterialColorToVec4(material.Colour.Emissive);
            _power = material.Colour.Power;
        }

        private void BuildStageTextures(Material material, TextureListChunk? textureList)
        {
            if (textureList == null)
                return;

            var stage = 0;
            var previousIndex = -1;

            foreach (var description in material.TextureMap.Descriptions)
            {
                // 0x05 and 0x06 are the colour and alpha operations on one sampler, so
                // an 0x06 repeating the previous index is the same texture, not a new stage.
                var op = description.Type & 0xFF;

                if (op == 0x06 && description.Index == previousIndex)
                    continue;

                if (description.Index >= 0 && description.Index < textureList.Textures.Count && stage < StageCount)
                {
                    _stageTextures[stage] = textureList.Textures[description.Index].Name;
                    _stageOffsets[stage] = description.Offset;
                    stage++;
                }

                previousIndex = description.Index;
            }
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
        }

        public bool DrawGuest(
            SlPassContext ctx, GuestDrawContext guest, TextureManager textureManager,
            in GuestSceneState scene, ReadOnlySpan<Matrix4x4> instances)
        {
            if (!Visible) return true;

            if (GuestMaterial is not { } material)
                return false;

            var state = ApplyMaterial(in scene);
            var binding = BindStages(ctx, guest, textureManager, material, in state);

            foreach (var world in instances)
            {
                guest.BindInstance(ctx, in binding, in state, world, _stageOffsets);
                ctx.DrawIndexed(_geometry.IndexCount);
            }

            return true;
        }

        public void DrawGuestInstance(
            SlPassContext ctx, GuestDrawContext guest, TextureManager textureManager,
            in GuestSceneState scene, in Matrix4x4 world)
        {
            if (GuestMaterial is not { } material)
                return;

            var state = ApplyMaterial(in scene);
            var binding = BindStages(ctx, guest, textureManager, material, in state);

            guest.BindInstance(ctx, in binding, in state, world, _stageOffsets);
            ctx.DrawIndexed(_geometry.IndexCount);
        }

        private GuestSceneState ApplyMaterial(in GuestSceneState scene)
        {
            var state = scene;

            state.MaterialDiffuse = _diffuse;
            state.MaterialAmbient = _ambient;
            state.MaterialSpecular = _specular;
            state.MaterialEmission = _emission;
            state.MaterialPower = _power;
            state.AlphaThreshold = _state.AlphaThreshold;

            return state;
        }

        private GuestBinding BindStages(
            SlPassContext ctx, GuestDrawContext guest, TextureManager textureManager,
            GuestMaterial material, in GuestSceneState state)
        {
            Span<SlTextureIndex> stages = stackalloc SlTextureIndex[StageCount];

            for (var i = 0; i < StageCount; i++)
            {
                // An unbound stage samples white rather than black: guest shaders
                // multiply these in, so black would blank the surface.
                stages[i] = _stageTextures[i] is { } name
                    ? ResolveIndex(textureManager, name)
                    : SlTextureIndex.WhiteTexture2D;
            }

            var binding = guest.BindMaterial(ctx, material, Bucket, _state, in state, stages, _stageOffsets);

            _geometry.Bind(ctx, slot: 0, GuestMaterial.VertexStride);

            return binding;
        }

        private static SlTextureIndex ResolveIndex(TextureManager textureManager, string? name)
        {
            var index = textureManager.GetIndex(name);

            return index == SlTextureIndex.NullTexture2D
                ? SlTextureIndex.WhiteTexture2D
                : index;
        }

        public void Dispose()
        {
            _geometry.Dispose();
        }
    }
}