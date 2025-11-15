using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using Silk.NET.WebGPU;
using XNOEdit.Shaders;

namespace XNOEdit.Renderer
{
    public unsafe class Model : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;
        private readonly List<ModelMesh> _meshes = [];
        private readonly Dictionary<int, WgpuBuffer<float>> _sharedVertexBuffers = new();
        private readonly ShaderArchive _shaderArchive;

        public Model(
            WebGPU wgpu,
            Device* device,
            ObjectChunk objectChunk,
            EffectListChunk effectListChunk,
            ShaderArchive shaderArchive)
        {
            _wgpu = wgpu;
            _device = device;
            _shaderArchive = shaderArchive;

            LoadModel(objectChunk, effectListChunk);
        }

        private void LoadModel(ObjectChunk objectChunk, EffectListChunk effectListChunk)
        {
            // Create shared vertex buffers for each unique VertexList
            for (int i = 0; i < objectChunk.VertexLists.Count; i++)
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
                }

                var vbo = new WgpuBuffer<float>(
                    _wgpu,
                    _device,
                    vertices.ToArray(),
                    BufferUsage.Vertex);
                _sharedVertexBuffers[i] = vbo;
            }

            foreach (var subObject in objectChunk.SubObjects)
            {
                foreach (var meshSet in subObject.MeshSets)
                {
                    var vertexListIndex = meshSet.VertexListIndex;
                    var primitiveList = meshSet.GetPrimitiveList(objectChunk);
                    var technique = meshSet.GetTechnique(effectListChunk);
                    var effectName = string.Empty;
                    var techniqueName = string.Empty;

                    if (technique != null)
                    {
                        var effect = technique.GetEffect(effectListChunk);

                        effectName = effect.Name;
                        techniqueName = technique.Name;
                    }

                    if (primitiveList == null || !_sharedVertexBuffers.TryGetValue(vertexListIndex, out var buffer))
                    {
                        continue;
                    }

                    var mesh = new ModelMesh(_wgpu, _device, buffer, primitiveList, effectName, techniqueName);
                    _meshes.Add(mesh);
                }
            }

            Console.WriteLine($"Loaded {_sharedVertexBuffers.Count} vertex buffers, {_meshes.Count} meshes");
        }

        public void Draw(RenderPassEncoder* passEncoder)
        {
            foreach (var mesh in _meshes)
            {
                mesh.Draw(passEncoder);
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
        private readonly WebGPU _wgpu;
        private WgpuBuffer<ushort> _indexBuffer;
        private WgpuBuffer<float> _vertexBuffer;
        private int _indexCount;
        private string _effect;
        private string _technique;

        public ModelMesh(
            WebGPU wgpu,
            Device* device,
            WgpuBuffer<float> sharedVbo,
            PrimitiveList primitiveList,
            string effect,
            string technique)
        {
            _wgpu = wgpu;
            _effect = effect;
            _technique = technique;
            _vertexBuffer = sharedVbo;

            LoadMesh(device, primitiveList);
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
        }

        public void Draw(RenderPassEncoder* passEncoder)
        {
            if (_indexCount == 0) return;

            _wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, 0, _vertexBuffer.Handle, 0, _vertexBuffer.Size);
            _wgpu.RenderPassEncoderSetIndexBuffer(passEncoder, _indexBuffer.Handle, IndexFormat.Uint16, 0, _indexBuffer.Size);
            _wgpu.RenderPassEncoderDrawIndexed(passEncoder, (uint)_indexCount, 1, 0, 0, 0);
        }

        public void Dispose()
        {
            _indexBuffer?.Dispose();
        }
    }
}
