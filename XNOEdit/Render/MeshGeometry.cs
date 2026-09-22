using Solaris;
using Solaris.Graph;

namespace XNOEdit.Render
{
    /// <summary>
    /// Manages vertex and index buffers for mesh geometry
    /// </summary>
    public class MeshGeometry : IDisposable
    {
        private SlBuffer? _sharedVertexBuffer;
        private SlBuffer? _indexBuffer;

        public uint IndexCount { get; private set; }

        public void Bind(SlPassContext ctx, uint slot, uint stride)
        {
            ctx.SetVertexBuffer(slot, _sharedVertexBuffer!.View, stride);
            ctx.SetIndexBuffer(_indexBuffer!.View);
        }

        public void Dispose()
        {
            _indexBuffer?.Dispose();
            // Don't dispose shared vertex buffer - it's owned elsewhere
        }

        /// <summary>
        /// Creates geometry from triangle strip indices (converts to triangle list)
        /// </summary>
        public static MeshGeometry CreateFromTriangleStrip(
            SlDevice device,
            SlBuffer sharedVertexBuffer,
            List<ushort> stripLengths,
            List<ushort> indices)
        {
            var triangleIndices = ConvertStripsToTriangles(stripLengths, indices);
            return CreateWithSharedVertices(device, sharedVertexBuffer, triangleIndices);
        }

        /// <summary>
        /// Converts triangle strip indices to triangle list, handling degenerate triangles
        /// </summary>
        private static ushort[] ConvertStripsToTriangles(List<ushort> stripLengths, List<ushort> indices)
        {
            var triangles = new List<ushort>();
            var offset = 0;

            foreach (int stripLength in stripLengths)
            {
                var count = stripLength - 2;

                for (var i = 0; i < count / 2; i++)
                {
                    var idx = offset + i * 2;

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
        /// Creates geometry using a shared vertex buffer with owned index buffer
        /// </summary>
        private static MeshGeometry CreateWithSharedVertices(
            SlDevice device,
            SlBuffer sharedVertexBuffer,
            ushort[] indices)
        {
            var geometry = new MeshGeometry();
            geometry._sharedVertexBuffer = sharedVertexBuffer;
            geometry.SetIndices(device, indices);
            return geometry;
        }

        private void SetIndices(SlDevice device, ushort[] indices)
        {
            IndexCount = (uint)indices.Length;
            _indexBuffer = device.CreateBuffer(indices, SlBufferUsage.Index);
        }
    }
}
