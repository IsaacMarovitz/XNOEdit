using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    /// <summary>
    /// Manages vertex and index buffers for mesh geometry
    /// </summary>
    public unsafe class MeshGeometry : IDisposable
    {
        private readonly WebGPU _wgpu;

        private WgpuBuffer<float>? _ownedVertexBuffer;
        private WgpuBuffer<float>? _sharedVertexBuffer;
        private WgpuBuffer<ushort>? _indexBuffer;
        private WgpuBuffer<ushort>? _wireframeIndexBuffer;

        public uint IndexCount { get; private set; }
        public uint WireframeIndexCount { get; private set; }
        public bool OwnsVertexBuffer => _ownedVertexBuffer != null;

        public MeshGeometry(WebGPU wgpu)
        {
            _wgpu = wgpu;
        }

        /// <summary>
        /// Creates owned vertex and index buffers from vertex/index arrays
        /// </summary>
        public static MeshGeometry Create<TVertex>(
            WebGPU wgpu,
            Device* device,
            TVertex[] vertices,
            ushort[] indices,
            bool generateWireframe = false) where TVertex : unmanaged
        {
            var geometry = new MeshGeometry(wgpu);

            // Convert vertices to float array
            var floatData = MemoryMarshal.Cast<TVertex, float>(vertices).ToArray();
            geometry._ownedVertexBuffer = new WgpuBuffer<float>(wgpu, device, floatData, BufferUsage.Vertex);

            geometry.SetIndices(device, indices, generateWireframe);

            return geometry;
        }

        /// <summary>
        /// Creates geometry using a shared vertex buffer with owned index buffer
        /// </summary>
        public static MeshGeometry CreateWithSharedVertices(
            WebGPU wgpu,
            Device* device,
            WgpuBuffer<float> sharedVertexBuffer,
            ushort[] indices,
            bool generateWireframe = false)
        {
            var geometry = new MeshGeometry(wgpu);
            geometry._sharedVertexBuffer = sharedVertexBuffer;
            geometry.SetIndices(device, indices, generateWireframe);
            return geometry;
        }

        /// <summary>
        /// Creates geometry from triangle strip indices (converts to triangle list)
        /// </summary>
        public static MeshGeometry CreateFromTriangleStrip(
            WebGPU wgpu,
            Device* device,
            WgpuBuffer<float> sharedVertexBuffer,
            IReadOnlyList<int> stripIndices,
            bool generateWireframe = true)
        {
            var triangleIndices = ConvertStripToTriangles(stripIndices);
            return CreateWithSharedVertices(wgpu, device, sharedVertexBuffer, triangleIndices, generateWireframe);
        }

        private void SetIndices(Device* device, ushort[] indices, bool generateWireframe)
        {
            IndexCount = (uint)indices.Length;
            _indexBuffer = new WgpuBuffer<ushort>(_wgpu, device, indices, BufferUsage.Index);

            if (generateWireframe)
            {
                var wireframeIndices = GenerateWireframeIndices(indices);
                WireframeIndexCount = (uint)wireframeIndices.Length;
                _wireframeIndexBuffer = new WgpuBuffer<ushort>(_wgpu, device, wireframeIndices, BufferUsage.Index);
            }
        }

        /// <summary>
        /// Converts triangle strip indices to triangle list, handling degenerate triangles
        /// </summary>
        public static ushort[] ConvertStripToTriangles(IReadOnlyList<int> stripIndices)
        {
            var triangles = new List<int>();

            for (var i = 0; i + 2 < stripIndices.Count; i++)
            {
                int a = stripIndices[i], b = stripIndices[i + 1], c = stripIndices[i + 2];

                // Skip degenerate triangles
                if (a == b || b == c || c == a)
                    continue;

                // Handle alternating winding order in strips
                if (i % 2 == 0)
                {
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
                else
                {
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(a);
                }
            }

            // Fix winding order
            for (var i = 0; i < triangles.Count; i += 3)
            {
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }

            return triangles.Select(x => (ushort)x).ToArray();
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
        public void BindVertexBuffer(RenderPassEncoder* passEncoder, uint slot = 0)
        {
            var buffer = _ownedVertexBuffer ?? _sharedVertexBuffer;
            if (buffer == null) return;

            _wgpu.RenderPassEncoderSetVertexBuffer(passEncoder, slot, buffer.Handle, 0, buffer.Size);
        }

        /// <summary>
        /// Draws the geometry (triangles or wireframe)
        /// </summary>
        public void Draw(RenderPassEncoder* passEncoder, bool wireframe = false)
        {
            if (wireframe)
            {
                if (_wireframeIndexBuffer == null || WireframeIndexCount == 0) return;

                _wgpu.RenderPassEncoderSetIndexBuffer(
                    passEncoder,
                    _wireframeIndexBuffer.Handle,
                    IndexFormat.Uint16,
                    0,
                    _wireframeIndexBuffer.Size);
                _wgpu.RenderPassEncoderDrawIndexed(passEncoder, WireframeIndexCount, 1, 0, 0, 0);
            }
            else
            {
                if (_indexBuffer == null || IndexCount == 0) return;

                _wgpu.RenderPassEncoderSetIndexBuffer(
                    passEncoder,
                    _indexBuffer.Handle,
                    IndexFormat.Uint16,
                    0,
                    _indexBuffer.Size);
                _wgpu.RenderPassEncoderDrawIndexed(passEncoder, IndexCount, 1, 0, 0, 0);
            }
        }

        /// <summary>
        /// Binds and draws in one call
        /// </summary>
        public void BindAndDraw(RenderPassEncoder* passEncoder, bool wireframe = false, uint vertexSlot = 0)
        {
            BindVertexBuffer(passEncoder, vertexSlot);
            Draw(passEncoder, wireframe);
        }

        public void Dispose()
        {
            _ownedVertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            _wireframeIndexBuffer?.Dispose();
            // Don't dispose shared vertex buffer - it's owned elsewhere
        }
    }
}
