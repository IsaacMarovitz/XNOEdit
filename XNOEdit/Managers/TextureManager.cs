using Silk.NET.WebGPU;
using XNOEdit.Renderer;

namespace XNOEdit.Managers
{
    public unsafe class TextureManager : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;
        private readonly ImGuiController _imguiController;

        private readonly Dictionary<string, ManagedTexture> _textures = new();
        private bool _disposed;

        public TextureManager(WebGPU wgpu, Device* device, ImGuiController imguiController)
        {
            _wgpu = wgpu;
            _device = device;
            _imguiController = imguiController;
        }

        public void Add(string name, Texture* texture, TextureView* view)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TextureManager));

            if (_textures.ContainsKey(name))
            {
                return;
            }

            // Create ImGui bind group for this texture
            _imguiController.BindImGuiTextureView(view);

            _textures[name] = new ManagedTexture
            {
                Texture = texture,
                View = view
            };
        }

        public void AddRange(IEnumerable<(string Name, IntPtr Texture, IntPtr View)> textures)
        {
            foreach (var (name, texture, view) in textures)
            {
                Add(name, (Texture*)texture, (TextureView*)view);
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

        public TextureView* GetView(string? name)
        {
            if (name != null && _textures.TryGetValue(name, out var texture))
                return texture.View;
            return null;
        }

        public bool TryGetView(string name, out TextureView* view)
        {
            if (_textures.TryGetValue(name, out var texture))
            {
                view = texture.View;
                return true;
            }
            view = null;
            return false;
        }

        public nint GetImGuiId(string name)
        {
            if (_textures.TryGetValue(name, out var texture))
                return (nint)texture.View;
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

        private void ReleaseTexture(ManagedTexture texture)
        {
            // Unbind from ImGui first
            _imguiController.UnbindImGuiTextureView(texture.View);

            // Release WebGPU resources
            if (texture.View != null)
                _wgpu.TextureViewRelease(texture.View);

            if (texture.Texture != null)
            {
                _wgpu.TextureDestroy(texture.Texture);
                _wgpu.TextureRelease(texture.Texture);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Clear();
        }

        private struct ManagedTexture
        {
            public Texture* Texture;
            public TextureView* View;
        }
    }

    public static unsafe class TextureManagerExtensions
    {
        public static TextureView* ResolveTexture(this TextureManager manager, string? name)
        {
            return manager.GetView(name);
        }
    }
}
