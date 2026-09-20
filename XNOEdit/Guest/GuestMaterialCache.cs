using Marathon.Formats.Archive;
using Solaris;
using XNOEdit.Logging;

namespace XNOEdit.Guest
{
    public sealed class GuestMaterialCache : IDisposable
    {
        private readonly SlDevice _device;
        private readonly GuestShaderCache _cache;
        private readonly ArcFile _shaderArchive;

        private readonly Dictionary<string, GuestMaterial?> _materials = [];

        public GuestMaterialCache(SlDevice device, GuestShaderCache cache, ArcFile shaderArchive)
        {
            _device = device;
            _cache = cache;
            _shaderArchive = shaderArchive;
        }

        public GuestMaterial? Resolve(string? effectName, string? techniqueName)
        {
            if (string.IsNullOrEmpty(effectName))
                return null;

            var directory = MapTechnique(techniqueName);
            var path = $"xenon/shader/{directory}/{effectName}o";

            if (_materials.TryGetValue(path, out var cached))
                return cached;

            GuestMaterial? material = null;

            try
            {
                var file = _shaderArchive.GetFile(path);

                if (file != null)
                {
                    using var stream = file.Decompress().Open();
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);

                    material = GuestMaterial.Create(
                        _device, _cache, $"{directory}/{effectName}", memory.ToArray());
                }
                else
                {
                    Logger.Debug?.PrintMsg(LogClass.Application, $"No '{path}' in shader.arc");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"Failed to load guest shader '{path}': {ex.Message}");
            }

            _materials[path] = material;
            return material;
        }

        private static string MapTechnique(string? techniqueName)
        {
            if (string.IsNullOrEmpty(techniqueName))
                return "std";

            return techniqueName.ToLowerInvariant() switch
            {
                "std" or "default" => "std",
                "std_np" => "std_np",
                "lm" => "lm",
                "lm_np" => "lm_np",
                "skin" => "skin",
                "morph" => "morph",
                _ => "std",
            };
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
