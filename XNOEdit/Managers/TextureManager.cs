using Solaris.RHI;
using XNOEdit.Renderer;

namespace XNOEdit.Managers
{
    public class TextureManager(ImGuiController imguiController) : IDisposable
    {
        private readonly Dictionary<string, ManagedTexture> _textures = new();
        private bool _disposed;

        public void Add(string name, SlTexture texture, SlTextureView view)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextureManager));

            if (_textures.ContainsKey(name))
            {
                return;
            }

            // Create ImGui bind group for this texture
            imguiController.BindImGuiTextureView(view);

            _textures[name] = new ManagedTexture
            {
                Texture = texture,
                View = view
            };
        }

        public void AddRange(IEnumerable<(string Name, SlTexture Texture, SlTextureView View)> textures)
        {
            foreach (var (name, texture, view) in textures)
            {
                Add(name, texture, view);
            }
        }

        public bool Remove(string name)
        {
            if (_textures.TryGetValue(name, out var texture))
            {
                ReleaseTexture(texture);
                _textures.Remove(name);
                return true;
            }

            return false;
        }

        public SlTextureView? GetView(string? name)
        {
            if (name != null && _textures.TryGetValue(name, out var texture))
                return texture.View;
            return null;
        }

        public bool TryGetView(string name, out SlTextureView? view)
        {
            if (_textures.TryGetValue(name, out var texture))
            {
                view = texture.View;
                return true;
            }

            view = null;
            return false;
        }

        public unsafe nint GetImGuiId(string name)
        {
            if (_textures.TryGetValue(name, out var texture))
                return (nint)texture.View.GetHandle();
            return 0;
        }

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

        private unsafe void ReleaseTexture(ManagedTexture texture)
        {
            // Unbind from ImGui first
            imguiController.UnbindImGuiTextureView((IntPtr)texture.View.GetHandle());

            // Release resources
            texture.View?.Dispose();
            texture.Texture?.Dispose();
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
            public SlTextureView View;
        }
    }

    public static unsafe class TextureManagerExtensions
    {
        public static SlTextureView? ResolveTexture(this TextureManager manager, string? name)
        {
            return manager.GetView(name);
        }
    }
}
