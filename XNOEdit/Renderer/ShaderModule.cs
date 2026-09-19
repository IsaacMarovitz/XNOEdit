using Solaris;

namespace XNOEdit.Renderer
{
    public abstract class ShaderModule : IDisposable
    {
        private const string EntryPoint = "shaderMain";

        public SlMaterial Material { get; }

        protected ShaderModule(
            SlDevice device,
            ReadOnlySpan<byte> vertexBytecode,
            ReadOnlySpan<byte> pixelBytecode,
            string label,
            IReadOnlyDictionary<string, SlPipelineVariant> variants)
        {
            var vertexShader = SlShader.Create(device, vertexBytecode, EntryPoint);
            var pixelShader = SlShader.Create(device, pixelBytecode, EntryPoint);

            Material = new SlMaterial(device, vertexShader, pixelShader, CreateVertexLayout(), variants, label);
        }

        protected abstract SlVertexLayout CreateVertexLayout();

        public void Dispose()
        {
            Material.Dispose();
        }
    }
}
