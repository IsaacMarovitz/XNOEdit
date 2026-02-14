using Silk.NET.WebGPU;
using Solaris.RHI;
using Solaris.Wgpu;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace XNOEdit.Renderer
{
    /// <summary>
    /// Manages vertex and index buffers for mesh geometry
    /// </summary>
    public unsafe class MeshGeometry : IDisposable
    {
        private readonly SlDevice _device;

        private SlBuffer<float>? _sharedVertexBuffer;
        private SlBuffer<ushort>? _indexBuffer;
        private SlBuffer<ushort>? _wireframeIndexBuffer;

        public uint IndexCount { get; private set; }
        public uint WireframeIndexCount { get; private set; }

        // TODO: Clean this up
        private WebGPU _wgpu => (_device as WgpuDevice).Wgpu;

        public MeshGeometry(SlDevice device)
        {
            _device = device;
        }

        /// <summary>
        /// Creates geometry using a shared vertex buffer with owned index buffer
        /// </summary>
        public static MeshGeometry CreateWithSharedVertices(
            SlDevice device,
            SlBuffer<float> sharedVertexBuffer,
            ushort[] indices)
        {
            var geometry = new MeshGeometry(device);
            geometry._sharedVertexBuffer = sharedVertexBuffer;
            geometry.SetIndices(device, indices);
            return geometry;
        }

        /// <summary>
        /// Creates geometry from triangle strip indices (converts to triangle list)
        /// </summary>
        public static MeshGeometry CreateFromTriangleStrip(
            SlDevice device,
            SlBuffer<float> sharedVertexBuffer,
            List<ushort> stripLengths,
            List<ushort> indices)
        {
            var triangleIndices = ConvertStripsToTriangles(stripLengths, indices);
            return CreateWithSharedVertices(device, sharedVertexBuffer, triangleIndices);
        }

        private void SetIndices(SlDevice device, ushort[] indices)
        {
            IndexCount = (uint)indices.Length;
            _indexBuffer = device.CreateBuffer(indices, SlBufferUsage.Index);

            var wireframeIndices = GenerateWireframeIndices(indices);
            WireframeIndexCount = (uint)wireframeIndices.Length;
            _wireframeIndexBuffer = device.CreateBuffer(wireframeIndices, SlBufferUsage.Index);
        }

        /// <summary>
        /// Converts triangle strip indices to triangle list, handling degenerate triangles
        /// </summary>
        public static ushort[] ConvertStripsToTriangles(List<ushort> stripLengths, List<ushort> indices)
        {
            var triangles = new List<ushort>();
            var offset = 0;

            foreach (int stripLength in stripLengths)
            {
                var count = stripLength - 2;

                for (var i = 0; i < count / 2; i++)
                {
                    var idx = offset + (i * 2);

                    // First triangle
                    triangles.Add(indices[idx]);
                    triangles.Add(indices[idx + 1]);
                    triangles.Add(indices[idx + 2]);

                    // Second triangle
                    triangles.Add(indices[idx + 2]);
                    triangles.Add(indices[idx + 1]);
                    triangles.Add(indices[idx + 3]);
                }

                // Handle odd remaining triangle
                if (count % 2 != 0)
                {
                    triangles.Add(indices[offset + stripLength - 3]);
                    triangles.Add(indices[offset + stripLength - 2]);
                    triangles.Add(indices[offset + stripLength - 1]);
                }

                offset += stripLength;
            }

            // Filter degenerates
            return triangles
                .Chunk(3)
                .Where(t => t[0] != t[1] && t[0] != t[2] && t[1] != t[2])
                .SelectMany(t => t)
                .ToArray();
        }

        /// <summary>
        /// Generates wireframe (line list) indices from triangle indices
        /// </summary>
        public static ushort[] GenerateWireframeIndices(ushort[] triangleIndices)
        {
            var lines = new HashSet<(ushort, ushort)>();

            for (var i = 0; i < triangleIndices.Length; i += 3)
            {
                var v0 = triangleIndices[i];
                var v1 = triangleIndices[i + 1];
                var v2 = triangleIndices[i + 2];

                AddLine(lines, v0, v1);
                AddLine(lines, v1, v2);
                AddLine(lines, v2, v0);
            }

            var result = new List<ushort>(lines.Count * 2);
            foreach (var (a, b) in lines)
            {
                result.Add(a);
                result.Add(b);
            }

            return result.ToArray();
        }

        private static void AddLine(HashSet<(ushort, ushort)> lines, ushort a, ushort b)
        {
            // Always store with smaller index first to avoid duplicate reversed edges
            lines.Add(a < b ? (a, b) : (b, a));
        }

        /// <summary>
        /// Binds vertex buffer to the render pass
        /// </summary>
        public void BindVertexBuffer(RenderPassEncoder* passEncoder, uint slot)
        {
            if (_sharedVertexBuffer == null) return;

            _wgpu.RenderPassEncoderSetVertexBuffer(
                passEncoder, slot, (Buffer*)_sharedVertexBuffer.GetHandle(), 0, _sharedVertexBuffer.Size);
        }

        /// <summary>
        /// Draws the geometry (triangles or wireframe)
        /// </summary>
        public void Draw(RenderPassEncoder* passEncoder, bool wireframe = false, int instanceCount = 1)
        {
            if (wireframe)
            {
                if (_wireframeIndexBuffer == null || WireframeIndexCount == 0) return;

                _wgpu.RenderPassEncoderSetIndexBuffer(
                    passEncoder,
                    (Buffer*)_wireframeIndexBuffer.GetHandle(),
                    IndexFormat.Uint16,
                    0,
                    _wireframeIndexBuffer.Size);
                _wgpu.RenderPassEncoderDrawIndexed(passEncoder, WireframeIndexCount, (uint)instanceCount, 0, 0, 0);
            }
            else
            {
                if (_indexBuffer == null || IndexCount == 0) return;

                _wgpu.RenderPassEncoderSetIndexBuffer(
                    passEncoder,
                    (Buffer*)_indexBuffer.GetHandle(),
                    IndexFormat.Uint16,
                    0,
                    _indexBuffer.Size);

                _wgpu.RenderPassEncoderDrawIndexed(passEncoder, IndexCount, (uint)instanceCount, 0, 0, 0);
            }
        }

        public void Dispose()
        {
            _indexBuffer?.Dispose();
            _wireframeIndexBuffer?.Dispose();
            // Don't dispose shared vertex buffer - it's owned elsewhere
        }
    }
}
