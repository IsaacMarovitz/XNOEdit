using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using Solaris;
using Solaris.Graph;
using XNOEdit.Guest;
using XNOEdit.Logging;
using XNOEdit.Managers;

namespace XNOEdit.Render
{
    public class Model : IDisposable
    {
        private readonly SlDevice _device;
        private readonly List<ModelMesh> _meshes = [];
        private readonly List<ModelMesh> _sky = [];
        private readonly List<ModelMesh> _opaque = [];
        private readonly List<ModelMesh> _punchThrough = [];
        private readonly List<ModelMesh> _transparent = [];
        private readonly Dictionary<int, SlBuffer> _sharedVertexBuffers = new();
        private readonly GuestMaterialCache? _guestMaterials;

        public Model(
            SlDevice device,
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk,
            GuestMaterialCache? guestMaterial)
        {
            _device = device;
            _guestMaterials = guestMaterial;

            LoadModel(objectChunk, textureListChunk, effectListChunk);
        }

        public bool GetSubobjectVisible(int subobject)
        {
            var meshes = _meshes.Where(m => m.Subobject == subobject);
            return meshes.Any(m => m.Visible);
        }

        public bool GetMeshSetVisible(int subobject, int meshSet)
        {
            var mesh = _meshes.FirstOrDefault(m => m.Subobject == subobject && m.MeshSet == meshSet);
            return mesh?.Visible ?? true;
        }

        public bool GetAnyMeshVisible()
        {
            return _meshes.Any(m => m.Visible);
        }

        public void SetAllVisible(bool visible)
        {
            foreach (var mesh in _meshes)
                mesh.SetVisible(visible);
        }

        public void SetVisible(int subobject, int? meshSet, bool visibility)
        {
            foreach (var mesh in _meshes)
            {
                if (mesh.Subobject != subobject) continue;

                if (meshSet != null)
                {
                    if (mesh.MeshSet == meshSet)
                        mesh.SetVisible(visibility);
                }
                else
                {
                    mesh.SetVisible(visibility);
                }
            }
        }

        private void LoadModel(
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk)
        {
            // Create shared vertex buffers for each unique VertexList
            for (var i = 0; i < objectChunk.VertexLists.Count; i++)
            {
                var vertexList = objectChunk.VertexLists[i];
                var vertices = new List<float>();

                foreach (var vertex in vertexList.Vertices)
                {
                    // Position
                    var position = vertex.Position ?? Vector3.Zero / 100.0f;
                    vertices.Add(position.X);
                    vertices.Add(position.Y);
                    vertices.Add(position.Z);

                    // Normal
                    var normal = vertex.Normal ?? Vector3.UnitY;
                    vertices.Add(normal.X);
                    vertices.Add(normal.Y);
                    vertices.Add(normal.Z);

                    // Tangent
                    var tangent = vertex.Tangent ?? Vector3.Zero;
                    vertices.Add(tangent.X);
                    vertices.Add(tangent.Y);
                    vertices.Add(tangent.Z);

                    // Binormal
                    var binormal = vertex.Binormal ?? Vector3.Zero;
                    vertices.Add(binormal.X);
                    vertices.Add(binormal.Y);
                    vertices.Add(binormal.Z);

                    // Color (BGRA)
                    if (vertex.VertexColourA != null)
                    {
                        vertices.Add(vertex.VertexColourA.Value.B / 255f);
                        vertices.Add(vertex.VertexColourA.Value.G / 255f);
                        vertices.Add(vertex.VertexColourA.Value.R / 255f);
                        vertices.Add(vertex.VertexColourA.Value.A / 255f);
                    }
                    else
                    {
                        vertices.Add(1.0f);
                        vertices.Add(1.0f);
                        vertices.Add(1.0f);
                        vertices.Add(1.0f);
                    }

                    // UV
                    var coordinates = vertex.TextureCoordinates;

                    for (var uv = 0; uv < 4; uv++)
                    {
                        var coordinate = coordinates != null && uv < coordinates.Count
                            ? coordinates[uv]
                            : Vector2.Zero;

                        vertices.Add(coordinate.X);
                        vertices.Add(coordinate.Y);
                    }
                }

                var vbo = _device.CreateBuffer(vertices.ToArray(), SlBufferUsage.Vertex);
                _sharedVertexBuffers[i] = vbo;
            }

            for (var i = 0; i < objectChunk.SubObjects.Count; i++)
            {
                var subObject = objectChunk.SubObjects[i];

                for (var j = 0; j < subObject.MeshSets.Count; j++)
                {
                    try
                    {
                        var meshSet = subObject.MeshSets[j];

                        var vertexListIndex = meshSet.VertexListIndex;
                        var primitiveList = meshSet.GetPrimitiveList(objectChunk);
                        var material = meshSet.GetMaterial(objectChunk);
                        string? effectName = null;
                        string? techniqueName = null;

                        if (effectListChunk != null)
                        {
                            var technique = meshSet.GetTechnique(effectListChunk);

                            if (technique != null)
                            {
                                var effect = technique.GetEffect(effectListChunk);

                                effectName = effect.Name;
                                techniqueName = technique.Name;
                            }
                        }

                        if (primitiveList == null || !_sharedVertexBuffers.TryGetValue(vertexListIndex, out var buffer))
                        {
                            continue;
                        }

                        var guestMaterial = _guestMaterials?.Resolve(effectName, techniqueName);

                        if ((subObject.Type & 0xFF) == 0x02)
                        {
                            Logger.Debug?.PrintMsg(LogClass.Application,
                                $"  transparent blend src=0x{(uint)material.Logic.SourceBlend:X} " +
                                $"dst=0x{(uint)material.Logic.DestinationBlend:X} op=0x{(uint)material.Logic.BlendOperation:X}");
                        }

                        var mesh = new ModelMesh(
                            _device, buffer, primitiveList, textureListChunk, material, guestMaterial,
                            (GuestDrawBucket)(subObject.Type & 0xFF), subObject.MeshSets[j].Centre, i, j);

                        _meshes.Add(mesh);

                        if (mesh.GuestMaterial is { IsSky: true })
                        {
                            _sky.Add(mesh);
                        }
                        else
                        {
                            switch (mesh.Bucket)
                            {
                                case GuestDrawBucket.Transparent: _transparent.Add(mesh); break;
                                case GuestDrawBucket.PunchThrough: _punchThrough.Add(mesh); break;
                                default: _opaque.Add(mesh); break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error?.PrintMsg(LogClass.Application, ex.Message);
                    }
                }
            }

            Logger.Debug?.PrintMsg(LogClass.Application, $"Loaded {_sharedVertexBuffers.Count} vertex buffers, {_meshes.Count} meshes");
        }

        public int DrawGuest(
            SlPassContext ctx, GuestDrawContext guest, TextureManager textureManager,
            in GuestSceneState scene, ReadOnlySpan<Matrix4x4> instances, GuestDrawPhase phase)
        {
            var meshes = phase switch
            {
                GuestDrawPhase.Sky => _sky,
                GuestDrawPhase.PunchThrough => _punchThrough,
                _ => _opaque,
            };

            if (meshes.Count == 0)
                return 0;

            return DrawBucket(meshes, ctx, guest, textureManager, in scene, instances);
        }

        public void CollectTransparent(List<GuestTransparentDraw> sink, Matrix4x4 view, ReadOnlySpan<Matrix4x4> instances)
        {
            foreach (var mesh in _transparent)
            {
                if (!mesh.Visible || mesh.GuestMaterial == null)
                    continue;

                foreach (var world in instances)
                {
                    var centre = Vector3.Transform(mesh.Centre, world);
                    sink.Add(new GuestTransparentDraw(mesh, world, Vector3.Transform(centre, view).Z));
                }
            }
        }

        private static int DrawBucket(
            List<ModelMesh> meshes, SlPassContext ctx, GuestDrawContext guest,
            TextureManager textureManager, in GuestSceneState scene, ReadOnlySpan<Matrix4x4> instances)
        {
            var skipped = 0;

            foreach (var mesh in meshes)
            {
                if (!mesh.DrawGuest(ctx, guest, textureManager, in scene, instances))
                {
                    skipped++;
                }
            }

            return skipped;
        }

        public void Dispose()
        {
            foreach (var mesh in _meshes)
            {
                mesh.Dispose();
            }
            foreach (var vbo in _sharedVertexBuffers.Values)
            {
                vbo.Dispose();
            }
            _meshes.Clear();
            _sharedVertexBuffers.Clear();
        }
    }

    public readonly record struct GuestTransparentDraw(ModelMesh Mesh, Matrix4x4 World, float ViewDepth);

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
