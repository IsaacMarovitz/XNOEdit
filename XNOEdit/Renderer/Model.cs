using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using Silk.NET.OpenGL;

namespace XNOEdit.Renderer
{
    public class Model : IDisposable
    {
        private readonly GL _gl;
        private readonly List<ModelMesh> _meshes = [];
        private readonly Dictionary<int, BufferObject<float>> _sharedVertexBuffers = new();

        public Model(GL gl, ObjectChunk objectChunk)
        {
            _gl = gl;
            LoadModel(objectChunk);
        }

        private void LoadModel(ObjectChunk objectChunk)
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
                    // Check if this a Marathon bug
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

                var vbo = new BufferObject<float>(_gl, vertices.ToArray(), BufferTargetARB.ArrayBuffer);
                _sharedVertexBuffers[i] = vbo;
            }

            foreach (var subObject in objectChunk.SubObjects)
            {
                foreach (var meshSet in subObject.MeshSets)
                {
                    var vertexListIndex = meshSet.VertexListIndex;
                    var primitiveList = meshSet.GetPrimitiveList(objectChunk);

                    if (primitiveList == null || !_sharedVertexBuffers.TryGetValue(vertexListIndex, out var buffer))
                    {
                        continue;
                    }

                    var mesh = new ModelMesh(_gl, buffer, primitiveList);
                    _meshes.Add(mesh);
                }
            }

            Console.WriteLine($"Loaded {_sharedVertexBuffers.Count} vertex buffers, {_meshes.Count} meshes");
        }

        public void Draw(XeShader xeShader)
        {
            xeShader.Use();

            foreach (var mesh in _meshes)
            {
                mesh.Draw(xeShader);
            }

            _gl.BindVertexArray(0);
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

    public class ModelMesh : IDisposable
    {
        private readonly GL _gl;
        private uint _vaoHandle;
        private BufferObject<ushort> _ebo;
        private int _indexCount;

        public ModelMesh(GL gl, BufferObject<float> sharedVbo, PrimitiveList primitiveList)
        {
            _gl = gl;
            LoadMesh(sharedVbo, primitiveList);
        }

        private void LoadMesh(BufferObject<float> sharedVbo, PrimitiveList primitiveList)
        {
            var sourceIndices = primitiveList.IndexIndices.Select(x => (int)x).ToList();

            var tris = new List<int>();

            for (var i = 0; i + 2 < sourceIndices.Count; i++)
            {
                int a = sourceIndices[i], b = sourceIndices[i + 1], c = sourceIndices[i + 2];

                // Skip degenerate triangles
                if (a == b || b == c || c == a)
                {
                    continue;
                }

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

            _vaoHandle = _gl.GenVertexArray();
            _gl.BindVertexArray(_vaoHandle);

            sharedVbo.Bind();

            _ebo = new BufferObject<ushort>(_gl, finalIndices, BufferTargetARB.ElementArrayBuffer);
            _ebo.Bind();

            unsafe
            {
                // Position (location 0): 3 floats at offset 0
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false,
                    10 * sizeof(float), (void*)0);
                _gl.EnableVertexAttribArray(0);

                // Normal (location 1): 3 floats at offset 3
                _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false,
                    10 * sizeof(float), (void*)(3 * sizeof(float)));
                _gl.EnableVertexAttribArray(1);

                // Color (location 2): 4 floats at offset 6
                _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false,
                    10 * sizeof(float), (void*)(6 * sizeof(float)));
                _gl.EnableVertexAttribArray(2);
            }

            _gl.BindVertexArray(0);
        }

        public unsafe void Draw(XeShader xeShader)
        {
            if (_indexCount == 0) return;

            _gl.BindVertexArray(_vaoHandle);
            _gl.DrawElements(GLEnum.Triangles, (uint)_indexCount, DrawElementsType.UnsignedShort, null);
        }

        public void Dispose()
        {
            if (_vaoHandle != 0)
                _gl.DeleteVertexArray(_vaoHandle);

            _ebo?.Dispose();
        }
    }
}
