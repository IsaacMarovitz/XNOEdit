using Solaris;

namespace XNOEdit.Services
{
    public struct LoadedTexture
    {
        public readonly string Name;
        public readonly SlTexture Texture;

        public LoadedTexture(string name, SlTexture texture)
        {
            Name = name;
            Texture = texture;
        }
    }
}

