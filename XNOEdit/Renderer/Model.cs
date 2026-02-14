using System.Numerics;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using Solaris;
using XNOEdit.Logging;
using XNOEdit.Managers;
using XNOEdit.Renderer.Shaders;

namespace XNOEdit.Renderer
{
    public struct TextureSet
    {
        public string? MainTexture;
        public string? BlendMap;
        public string? NormalMap;
        public string? LightMap;
        public bool Specular;
    }

    public class Model : IDisposable
    {
        private readonly SlDevice _device;
        private readonly List<ModelMesh> _meshes = [];
        private readonly Dictionary<int, SlBuffer<float>> _sharedVertexBuffers = new();
        private readonly ArcFile _shaderArchive;

        public Model(
            SlDevice device,
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk,
            ArcFile shaderArchive,
            ModelShader shader)
        {
            _device = device;
            _shaderArchive = shaderArchive;

            LoadModel(objectChunk, textureListChunk, effectListChunk, shader);
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
            EffectListChunk effectListChunk,
            ModelShader shader)
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
                    if (vertex.TextureCoordinates != null)
                    {
                        if (vertex.TextureCoordinates.Count >= 2)
                        {
                            vertices.Add(vertex.TextureCoordinates[0].X);
                            vertices.Add(vertex.TextureCoordinates[0].Y);

                            vertices.Add(vertex.TextureCoordinates[1].X);
                            vertices.Add(vertex.TextureCoordinates[1].Y);
                        }
                        else if (vertex.TextureCoordinates.Count >= 1)
                        {
                            vertices.Add(vertex.TextureCoordinates[0].X);
                            vertices.Add(vertex.TextureCoordinates[0].Y);

                            vertices.Add(1.0f);
                            vertices.Add(1.0f);
                        }
                    }
                    else
                    {
                        vertices.Add(1.0f);
                        vertices.Add(1.0f);

                        vertices.Add(1.0f);
                        vertices.Add(1.0f);
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
                        var textureSet = new TextureSet();
                        string effectName = null;
                        string techniqueName = null;

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

                        if (textureListChunk != null)
                        {
                            var textures = material.TextureMap.Descriptions
                                .Where(d => d.Index >= 0 && d.Index < textureListChunk.Textures.Count)
                                .Select(d => textureListChunk.Textures[d.Index])
                                .Distinct()
                                .ToList();

                            textureSet = IntuitTextures(textures);
                        }

                        if (primitiveList == null || !_sharedVertexBuffers.TryGetValue(vertexListIndex, out var buffer))
                        {
                            continue;
                        }

                        if (effectName != null)
                        {
                            var shaderFile = _shaderArchive.GetFile($"xenon/shader/std/{effectName}o");
                            // var shaderData = shaderFile.Decompress
                            // var containers = ShaderArchive.ExtractShaderContainers(shaderData);
                        }

                        var mesh = new ModelMesh(_device, buffer, primitiveList, textureSet, material, shader, i, j);
                        _meshes.Add(mesh);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error?.PrintMsg(LogClass.Application, ex.Message);
                    }
                }
            }

            Logger.Debug?.PrintMsg(LogClass.Application, $"Loaded {_sharedVertexBuffers.Count} vertex buffers, {_meshes.Count} meshes");
        }

        private static TextureSet IntuitTextures(List<TextureFile> textures)
        {
            // Adapted from Beatz
            int FindDiffuseIndex(string lowerName, out string mainTag)
            {
                string[] diffuseTags = ["_dfxx", "_dfsp", "_dfpt", "_df"]; // Order matters, longer first
                foreach (var tag in diffuseTags)
                {
                    var idx = lowerName.IndexOf(tag);
                    if (idx >= 0)
                    {
                        mainTag = tag;
                        return idx;
                    }
                }

                mainTag = null;
                return -1;
            }

            var diffuseTextures = textures.Where(t =>
            {
                var lower = t.Name.ToLowerInvariant();
                return FindDiffuseIndex(lower, out _) >= 0;
            }).ToList();

            var mainTextureName = diffuseTextures.FirstOrDefault()?.Name;
            var blendMapName = diffuseTextures.Count > 1 ? diffuseTextures.Last().Name : null;

            var normalMapName = textures.FirstOrDefault(t =>
            {
                var lower = t.Name.ToLowerInvariant();
                return lower.Contains("_ntxx") || lower.Contains("_nw") || lower.Contains("_nt");
            })?.Name;

            var lightMapName = textures.FirstOrDefault(t =>
            {
                var lower = t.Name.ToLowerInvariant();
                return lower.Contains("_lm") || lower.Contains("_zlm");
            })?.Name;

            // Fallback main tex if no diffuse found
            if (mainTextureName == null)
            {
                mainTextureName = textures.FirstOrDefault(t =>
                    !t.Name.ToLowerInvariant().Contains("_lm") &&
                    !t.Name.ToLowerInvariant().Contains("_zlm") &&
                    !t.Name.ToLowerInvariant().Contains("_ntxx") &&
                    !t.Name.ToLowerInvariant().Contains("_nw") &&
                    !t.Name.ToLowerInvariant().Contains("_nt"))?.Name ?? textures.FirstOrDefault()?.Name;
            }

            var specular = false;

            if (mainTextureName != null)
            {
                Logger.Debug?.PrintMsg(LogClass.Application, $"Main Texture: {mainTextureName}");

                if (mainTextureName.Contains("_dfsp"))
                {
                    specular = true;
                }
            }

            if (blendMapName != null)
            {
                Logger.Debug?.PrintMsg(LogClass.Application, $"Blend Map: {blendMapName}");

                if (blendMapName.Contains("_dfsp"))
                {
                    specular = true;
                }
            }

            if (normalMapName != null)
                Logger.Debug?.PrintMsg(LogClass.Application, $"Normal Map: {normalMapName}");

            if (lightMapName != null)
                Logger.Debug?.PrintMsg(LogClass.Application, $"Light Map: {lightMapName}");

            return new TextureSet
            {
                MainTexture = mainTextureName,
                BlendMap = blendMapName,
                NormalMap = normalMapName,
                LightMap = lightMapName,
                Specular = specular,
            };
        }

        public void Draw(
            SlRenderPass passEncoder,
            bool wireframe,
            TextureManager textureManager,
            ModelShader shader,
            int instanceCount = 1)
        {
            foreach (var mesh in _meshes)
            {
                mesh.Draw(passEncoder, wireframe, textureManager, shader, instanceCount);
            }
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

    public unsafe class ModelMesh : IDisposable
    {
        public int Subobject { get; private set; }
        public int MeshSet { get; private set; }
        public bool Visible { get; private set; } = true;

        private readonly SlDevice _device;
        private readonly MeshGeometry _geometry;
        private TextureSet _textureSet;
        private SlBuffer<PerMeshUniforms> _meshUniformBuffer;
        private SlBindGroup _meshBindGroup;
        private SlBindGroup _textureBindGroup;

        public ModelMesh(
            SlDevice device,
            SlBuffer<float> sharedVbo,
            PrimitiveList primitiveList,
            TextureSet textureSet,
            Material material,
            ModelShader shader,
            int subobject,
            int meshSet)
        {
            _device = device;
            _textureSet = textureSet;
            Subobject = subobject;
            MeshSet = meshSet;

            _geometry = MeshGeometry.CreateFromTriangleStrip(
                device, sharedVbo, primitiveList.StripIndices, primitiveList.IndexIndices);

            CreateMeshUniforms(device, material, shader);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
        }

        private void CreateMeshUniforms(SlDevice device, Material material, ModelShader shader)
        {
            _meshUniformBuffer = device.CreateUniform<PerMeshUniforms>();

            var meshUniforms = new PerMeshUniforms
            {
                AmbientColor = PropertyUtility.MaterialColorToVec4(material.Colour.Ambient),
                DiffuseColor = PropertyUtility.MaterialColorToVec4(material.Colour.Diffuse),
                SpecularColor = PropertyUtility.MaterialColorToVec4(material.Colour.Specular),
                EmissiveColor = PropertyUtility.MaterialColorToVec4(material.Colour.Emissive),
                SpecularPower = material.Colour.Power,
                AlphaRef = material.Logic.AlphaRef / 255.0f,
                Alpha = material.Logic.Alpha ? 1.0f : 0.0f,
                Blend = material.Logic.Blend ? 1.0f : 0.0f,
                Specular = _textureSet.Specular ? 1.0f : 0.0f,
            };

            var queue = device.GetQueue();
            _meshUniformBuffer.UpdateData(queue, in meshUniforms);
            _meshBindGroup = shader.CreatePerMeshBindGroup(_meshUniformBuffer);
            queue.Dispose();
        }

        public void Draw(
            SlRenderPass passEncoder,
            bool wireframe,
            TextureManager textureManager,
            ModelShader shader,
            int instanceCount = 1)
        {
            if (!Visible) return;

            _geometry.BindVertexBuffer(passEncoder, 0);
            passEncoder.SetBindGroup(1, _meshBindGroup);

            var mainTexture = textureManager.GetView(_textureSet.MainTexture);
            var blendTexture = textureManager.GetView(_textureSet.BlendMap);
            var normalTexture = textureManager.GetView(_textureSet.NormalMap);
            var lightmapTexture = textureManager.GetView(_textureSet.LightMap);

            _textureBindGroup?.Dispose();

            _textureBindGroup = shader.GetTextureBindGroup(mainTexture, blendTexture, normalTexture, lightmapTexture);
            passEncoder.SetBindGroup(2, _textureBindGroup);

            _geometry.Draw(passEncoder, wireframe, instanceCount);
        }

        public void Dispose()
        {
            _geometry.Dispose();
            _meshUniformBuffer?.Dispose();
            _meshBindGroup?.Dispose();
        }
    }
}
