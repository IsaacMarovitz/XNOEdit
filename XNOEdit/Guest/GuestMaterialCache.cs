using Marathon.Formats.Archive;
using Solaris;
using XNOEdit.Logging;

namespace XNOEdit.Guest
{
    public sealed class GuestMaterialCache : IDisposable
    {
        private readonly SlDevice _device;
        private readonly GuestShaderCache _cache;
        private readonly GuestTechniqueTable _techniques;

        private readonly Dictionary<(string Path, (int Vertex, int Pixel) Indices), GuestMaterial?> _materials = [];

        public GuestMaterialCache(SlDevice device, GuestShaderCache cache, GuestTechniqueTable techniques)
        {
            _device = device;
            _cache = cache;
            _techniques = techniques;
        }

        public GuestMaterial? Resolve(string? effectName, string? techniqueName)
        {
            if (string.IsNullOrEmpty(effectName))
                return null;

            var path = $"xenon/shader/std/{effectName}o";
            var indices = _techniques.Resolve(effectName, techniqueName);
            var key = (path, indices);

            if (_materials.TryGetValue(key, out var cached))
                return cached;

            GuestMaterial? material = null;

            try
            {
                var file = ArcFiles.ShaderArc.GetFile(path);

                if (file != null)
                {
                    using var stream = file.Decompress().Open();
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);

                    material = GuestMaterial.Create(
                        _device, _cache, path, memory.ToArray(), indices.Vertex, indices.Pixel);
                }
            }
            catch (Exception ex)
            {
                Logger.Error?.PrintMsg(LogClass.Application, $"Failed to load guest shader '{path}': {ex.Message}");
            }

            _materials[key] = material;
            return material;
        }

        public void Dispose()
        {
            foreach (var material in _materials.Values)
            {
                material?.Dispose();
            }

            _materials.Clear();
        }
    }
}
