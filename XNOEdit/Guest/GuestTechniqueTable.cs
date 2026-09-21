using XNOEdit.Logging;

namespace XNOEdit.Guest
{
    public sealed class GuestTechniqueTable
    {
        private const uint Magic = 0x53484354;

        private readonly Dictionary<(string Effect, string Technique), (int Vertex, int Pixel)> _entries = [];

        public static GuestTechniqueTable Load(Stream stream)
        {
            var table = new GuestTechniqueTable();
            using var reader = new BinaryReader(stream);

            if (reader.ReadUInt32() != Magic)
                throw new InvalidDataException("Not a shader technique table.");

            var count = reader.ReadUInt32();

            for (var i = 0u; i < count; i++)
            {
                var effect = reader.ReadString();
                var technique = reader.ReadString();
                var vertex = reader.ReadUInt16();
                var pixel = reader.ReadUInt16();

                table._entries[(effect, technique)] = (vertex, pixel);
            }

            return table;
        }

        public (int Vertex, int Pixel) Resolve(string effect, string? technique)
        {
            if (technique != null && _entries.TryGetValue((effect, technique), out var indices))
                return indices;

            Logger.Warning?.PrintMsg(LogClass.Application,
                $"No technique entry for '{effect}':'{technique}'; using the first shader pair");

            return (0, 0);
        }
    }
}
