using System.Buffers.Binary;
using System.Text;

namespace XNOEdit.Guest
{
    public readonly record struct GuestTechnique(string Name, int Vertex, int Pixel);

    public static class GuestEffect
    {
        private const int PointerBias = 20;
        private const int NameBias = 12;
        private const int ShaderQuadOffset = 0x3C;
        private const int TechniqueRecordSize = 20;

        public static List<GuestTechnique> ScanTechniques(byte[] file, List<GuestShaderContainer> containers)
        {
            var techniques = new List<GuestTechnique>();

            var vertex = containers.Where(c => c.Stage == GuestShaderStage.Vertex)
                .Select(c => c.Offset).ToList();
            var pixel = containers.Where(c => c.Stage == GuestShaderStage.Pixel)
                .Select(c => c.Offset).ToList();

            if (vertex.Count == 0 || pixel.Count == 0)
                return techniques;

            // Pass objects
            var passes = new Dictionary<int, (int Vertex, int Pixel)>();

            for (var i = 0; i + 8 <= file.Length; i += 4)
            {
                var vertexIndex = vertex.IndexOf(Read(file, i) + PointerBias);
                var pixelIndex = pixel.IndexOf(Read(file, i + 4) + PointerBias);

                if (vertexIndex >= 0 && pixelIndex >= 0)
                    passes[i - 4 - ShaderQuadOffset] = (vertexIndex, pixelIndex);
            }

            // Pass list entries
            var lists = new Dictionary<int, (int Vertex, int Pixel)>();

            for (var i = 0; i + 4 <= file.Length; i += 4)
            {
                if (passes.TryGetValue(Read(file, i) + PointerBias, out var indices))
                    lists[i] = indices;
            }

            // Technique records
            var previous = -TechniqueRecordSize;

            for (var i = 0; i + TechniqueRecordSize <= file.Length; i += 4)
            {
                if (!lists.TryGetValue(Read(file, i + 16) + PointerBias, out var indices))
                    continue;

                if (i - previous < TechniqueRecordSize)
                    continue;

                var name = ReadName(file, Read(file, i) + NameBias);

                if (name == null)
                    continue;

                previous = i;
                techniques.Add(new GuestTechnique(name, indices.Vertex, indices.Pixel));
            }

            return techniques;
        }

        private static int Read(byte[] file, int offset) =>
            (int)BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(offset));

        private static string? ReadName(byte[] file, int offset)
        {
            if (offset < 0 || offset >= file.Length)
                return null;

            var end = offset;

            while (end < file.Length && file[end] != 0)
            {
                if (file[end] < 32 || file[end] > 126)
                    return null;

                end++;
            }

            return end > offset ? Encoding.ASCII.GetString(file, offset, end - offset) : null;
        }
    }
}
