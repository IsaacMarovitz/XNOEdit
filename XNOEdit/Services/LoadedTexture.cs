using Solaris;

namespace XNOEdit.Services
{
    public struct LoadedTexture
    {
        public readonly string Name;
        public readonly SlTexture Texture;
        public readonly SlTextureView View;

        public LoadedTexture(string name, SlTexture texture, SlTextureView view)
        {
            Name = name;
            Texture = texture;
            View = view;
        }
    }
}
