using Solaris;

namespace XNOEdit.Managers
{
    public class TextureManager(SlDevice device) : IDisposable
    {
        private readonly Dictionary<string, ManagedTexture> _textures = new();
        private bool _disposed;

        public void Add(string name, SlTexture texture)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextureManager));

            if (_textures.ContainsKey(name))
            {
                return;
            }

            _textures[name] = new ManagedTexture
            {
                Texture = texture,
                Index = device.Tables.Register(texture)
            };
        }

        public void AddRange(IEnumerable<(string Name, SlTexture Texture)> textures)
        {
            foreach (var (name, texture) in textures)
            {
                Add(name, texture);
            }
        }

        /// <summary>
        /// Returns the bindless index, or the null-texture index when unknown. Callers
        /// put this straight into a push constant — there is no failure path to handle.
        /// </summary>
        public SlTextureIndex GetIndex(string? name)
        {
            if (name != null && _textures.TryGetValue(name, out var texture))
                return texture.Index;

            return SlTextureIndex.NullTexture2D;
        }

        public ulong GetImGuiTextureId(string? name) => GetIndex(name).Packed;

        public bool Contains(string name) => _textures.ContainsKey(name);

        public IEnumerable<string> Names => _textures.Keys;

        public int Count => _textures.Count;

        public void Clear()
        {
            foreach (var texture in _textures.Values)
            {
                ReleaseTexture(texture);
            }
            _textures.Clear();
        }

        private void ReleaseTexture(ManagedTexture texture)
        {
            device.Tables.Release(texture.Index);
            device.Retire(texture.Texture);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Clear();
        }

        private struct ManagedTexture
        {
            public SlTexture Texture;
            public SlTextureIndex Index;
        }
    }
}
