using Silk.NET.WebGPU;

namespace XNOEdit.Services
{
    public unsafe struct LoadedTexture
    {
        public string Name;
        public Texture* Texture;
        public TextureView* View;

        public LoadedTexture(string name, Texture* texture, TextureView* view)
        {
            Name = name;
            Texture = texture;
            View = view;
        }
    }
}
