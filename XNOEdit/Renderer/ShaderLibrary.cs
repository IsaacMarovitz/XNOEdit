using Plume;
using Solaris;

namespace XNOEdit.Renderer
{
    public static class ShaderLibrary
    {
        private static readonly Dictionary<string, byte[]> _cache = [];

        private static string Extension(SlDevice device)
        {
            return device.Capabilities.ShaderFormat switch
            {
                RenderShaderFormat.Metal => "metallib",
                RenderShaderFormat.Spirv => "spv",
                RenderShaderFormat.Dxil => "dxil",
                _ => throw new PlatformNotSupportedException("The device reported no usable shader format."),
            };
        }

        public static ReadOnlySpan<byte> Get(SlDevice device, string name)
        {
            if (_cache.TryGetValue(name, out var cached))
                return cached;

            var resource = $"XNOEdit.Shaders.{name}.{Extension(device)}";

            using var stream = typeof(ShaderLibrary).Assembly.GetManifestResourceStream(resource)
                               ?? throw new FileNotFoundException(
                                   $"Shader '{resource}' is not embedded. Available: " +
                                   string.Join(", ", typeof(ShaderLibrary).Assembly.GetManifestResourceNames()));

            using var memory = new MemoryStream();
            stream.CopyTo(memory);

            var bytes = memory.ToArray();
            _cache[name] = bytes;
            return bytes;
        }
    }
}
