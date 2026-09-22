using Solaris;

namespace XNOEdit.Render
{
    public abstract class ShaderModule : IDisposable
    {
        private const string EntryPoint = "shaderMain";

        public SlMaterial Material { get; }

        public SlPipelineVariant Variant { get; }

        protected ShaderModule(
            SlDevice device,
            ReadOnlySpan<byte> vertexBytecode,
            ReadOnlySpan<byte> pixelBytecode,
            string label,
            in SlPipelineVariant variant)
        {
            var vertexShader = SlShader.Create(device, vertexBytecode, EntryPoint);
            var pixelShader = SlShader.Create(device, pixelBytecode, EntryPoint);

            Variant = variant;
            Material = new SlMaterial(device, vertexShader, pixelShader, CreateVertexLayout(), label);
        }

        public SlPipeline Pipeline(in SlPassSignature signature)
        {
            var slPipelineVariant = Variant;
            return Material.Pipeline(in slPipelineVariant, in signature);
        }

        protected abstract SlVertexLayout CreateVertexLayout();

        public void Dispose()
        {
            Material.Dispose();
        }
    }
}
