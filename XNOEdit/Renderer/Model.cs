using System.Numerics;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using Silk.NET.WebGPU;
using XNOEdit.Logging;
using XNOEdit.Renderer.Shaders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    public struct TextureSet
    {
        public string MainTexture;
        public string BlendMap;
        public string NormalMap;
        public string LightMap;
        public bool Specular;
    }

    public unsafe class Model : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;
        private readonly List<ModelMesh> _meshes = [];
        private readonly Dictionary<int, WgpuBuffer<float>> _sharedVertexBuffers = new();
        private readonly ArcFile _shaderArchive;

        public Model(
            WebGPU wgpu,
            Device* device,
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk,
            ArcFile shaderArchive,
            ModelShader shader)
        {
            _wgpu = wgpu;
            _device = device;
            _shaderArchive = shaderArchive;

            LoadModel(objectChunk, textureListChunk, effectListChunk, shader);
        }

        public void SetVisible(int subobject, int? meshSet, bool visibility)
        {
            foreach (var mesh in _meshes)
            {
                if (mesh.Subobject != subobject) continue;

                if (meshSet != null)
                {
                    if (mesh.MeshSet == meshSet)
                    {
                        mesh.SetVisible(visibility);
                    }
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

                var vbo = new WgpuBuffer<float>(
                    _wgpu,
                    _device,
                    vertices.ToArray(),
                    BufferUsage.Vertex);
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

                        var mesh = new ModelMesh(_wgpu, _device, buffer, primitiveList, textureSet, material, shader, i, j);
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
            RenderPassEncoder* passEncoder,
            bool wireframe,
            IReadOnlyDictionary<string, IntPtr> textures,
            ModelShader shader)
        {
            foreach (var mesh in _meshes)
            {
                mesh.Draw(passEncoder, wireframe, textures, shader);
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

        private readonly WebGPU _wgpu;
        private WgpuBuffer<ushort> _indexBuffer;
        private WgpuBuffer<ushort> _wireframeIndexBuffer;
        private WgpuBuffer<float> _vertexBuffer;
        private int _indexCount;
        private int _wireframeIndexCount;
        private TextureSet _textureSet;
        private bool _visible = true;

        // Per-mesh uniform buffer and bind group (set once at creation)
        private WgpuBuffer<PerMeshUniforms> _meshUniformBuffer;
        private BindGroup* _meshBindGroup;

        public ModelMesh(
            WebGPU wgpu,
            Device* device,
            WgpuBuffer<float> sharedVbo,
            PrimitiveList primitiveList,
            TextureSet textureSet,
            Material material,
            ModelShader shader,
            int subobject,
            int meshSet)
        {
            _wgpu = wgpu;
            _vertexBuffer = sharedVbo;
            _textureSet = textureSet;

            Subobject = subobject;
            MeshSet = meshSet;

            LoadMesh(device, primitiveList);
            CreateMeshUniforms(wgpu, device, material, shader);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
        }

        private void CreateMeshUniforms(WebGPU wgpu, Device* device, Material material, ModelShader shader)
        {
            _meshUniformBuffer = WgpuBuffer<PerMeshUniforms>.CreateUniform(wgpu, device);

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

            var queue = wgpu.DeviceGetQueue(device);
            _meshUniformBuffer.UpdateData(queue, in meshUniforms);
            _meshBindGroup = shader.CreatePerMeshBindGroup(_meshUniformBuffer);
        }

        private void LoadMesh(Device* device, PrimitiveList primitiveList)
        {
            var sourceIndices = primitiveList.IndexIndices.Select(x => (int)x).ToList();
            var tris = new List<int>();

            for (var i = 0; i + 2 < sourceIndices.Count; i++)
            {
                int a = sourceIndices[i], b = sourceIndices[i + 1], c = sourceIndices[i + 2];

                // Skip degenerate triangles
                if (a == b || b == c || c == a)
                    continue;

                // Handle alternating winding order in strips
                tris.AddRange(i % 2 == 0 ? new[] { a, b, c } : new[] { c, b, a });
            }

            // Fix winding order
            for (var i = 0; i < tris.Count; i += 3)
            {
                (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }

            var finalIndices = tris.Select(x => (ushort)x).ToArray();
            _indexCount = finalIndices.Length;

            _indexBuffer = new WgpuBuffer<ushort>(
                _wgpu,
                device,
                finalIndices,
                BufferUsage.Index);

            // Generate wireframe indices (convert triangles to lines)
            var wireframeIndices = GenerateWireframeIndices(finalIndices);
            _wireframeIndexCount = wireframeIndices.Length;

            _wireframeIndexBuffer = new WgpuBuffer<ushort>(
                _wgpu,
                device,
                wireframeIndices,
                BufferUsage.Index);
        }

        private ushort[] GenerateWireframeIndices(ushort[] triangleIndices)
        {
            var lines = new HashSet<(ushort, ushort)>();

            // Convert each triangle to 3 lines, avoiding duplicates
            for (var i = 0; i < triangleIndices.Length; i += 3)
            {
                var v0 = triangleIndices[i];
                var v1 = triangleIndices[i + 1];
                var v2 = triangleIndices[i + 2];

                // Add edges, ensuring consistent ordering to avoid duplicates
                AddLine(lines, v0, v1);
                AddLine(lines, v1, v2);
                AddLine(lines, v2, v0);
            }

            // Flatten to array
            var result = new List<ushort>(lines.Count * 2);
            foreach (var (a, b) in lines)
            {
                result.Add(a);
                result.Add(b);
            }

            return result.ToArray();
        }

        private void AddLine(HashSet<(ushort, ushort)> lines, ushort a, ushort b)
        {
            // Always store with smaller index first to avoid duplicate reversed edges
            if (a < b)
                lines.Add((a, b));
            else
                lines.Add((b, a));
        }

        public void Draw(
            RenderPassEncoder* passEncoder,
            bool wireframe,
            IReadOnlyDictionary<string, IntPtr> textures,
            ModelShader shader)
        {
            if (!_visible) return;

            _wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 1, _meshBindGroup, 0, null);

            TextureView* mainTexture = null;
            if (_textureSet.MainTexture != null &&
                textures.TryGetValue(_textureSet.MainTexture, out var mainTexturePtr))
            {
                mainTexture = (TextureView*)mainTexturePtr;
            }

            TextureView* blendTexture = null;
            if (_textureSet.BlendMap != null &&
                textures.TryGetValue(_textureSet.BlendMap, out var blendTexturePtr))
            {
                blendTexture = (TextureView*)blendTexturePtr;
            }

            TextureView* normalTexture = null;
            if (_textureSet.NormalMap != null &&
                textures.TryGetValue(_textureSet.NormalMap, out var normalTexturePtr))
            {
                normalTexture = (TextureView*)normalTexturePtr;
            }

            TextureView* lightmapTexture = null;
            if (_textureSet.LightMap != null &&
                textures.TryGetValue(_textureSet.LightMap, out var lightmapTexturePtr))
            {
                lightmapTexture = (TextureView*)lightmapTexturePtr;
            }

            var textureBindGroup = shader.GetTextureBindGroup(
                mainTexture, blendTexture, normalTexture, lightmapTexture);
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 2, textureBindGroup, 0, null);

            if (wireframe)
            {
                if (_wireframeIndexCount == 0) return;
                _wgpu.RenderPassEncoderSetIndexBuffer(passEncoder, _wireframeIndexBuffer.Handle,
                    IndexFormat.Uint16, 0, _wireframeIndexBuffer.Size);
                _wgpu.RenderPassEncoderDrawIndexed(passEncoder, (uint)_wireframeIndexCount, 1, 0, 0, 0);
            }
            else
            {
                if (_indexCount == 0) return;
                _wgpu.RenderPassEncoderSetIndexBuffer(passEncoder, _indexBuffer.Handle,
                    IndexFormat.Uint16, 0, _indexBuffer.Size);
                _wgpu.RenderPassEncoderDrawIndexed(passEncoder, (uint)_indexCount, 1, 0, 0, 0);
            }
        }

        public void Dispose()
        {
            _indexBuffer?.Dispose();
            _wireframeIndexBuffer?.Dispose();
            _meshUniformBuffer?.Dispose();

            if (_meshBindGroup != null)
                _wgpu.BindGroupRelease(_meshBindGroup);
        }
    }
}
