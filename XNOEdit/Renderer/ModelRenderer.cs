using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Shaders;

namespace XNOEdit.Renderer
{
    public unsafe class ModelRenderer : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly ModelShader _shader;
        private readonly Model _model;

        public ModelRenderer(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            TextureFormat swapChainFormat,
            Model model)
        {
            _wgpu = wgpu;
            _model = model;

            _shader = new ModelShader(
                wgpu,
                device,
                queue,
                EmbeddedResources.ReadAllText("XNOEdit/Shaders/BasicModel.wgsl"),
                "Basic Model",
                swapChainFormat);
        }

        public void Draw(
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            Vector3 lightDir,
            Vector3 lightColor,
            Vector3 viewPos,
            float vertColorStrength,
            bool wireframe,
            bool cullBackfaces)
        {
            var uniforms = new BasicModelUniforms
            {
                Model = Matrix4x4.Identity,
                View = view,
                Projection = projection,
                LightDir = lightDir.AsVector4(),
                LightColor = lightColor.AsVector4(),
                ViewPos = viewPos,
                VertColorStrength = vertColorStrength
            };

            _shader.UpdateUniforms(in uniforms);

            var pipeline = _shader.GetPipeline(cullBackfaces, wireframe);
            _wgpu.RenderPassEncoderSetPipeline(passEncoder, pipeline);

            uint dynamicOffset = 0;
            _wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, _shader.BindGroup, 0, &dynamicOffset);

            _model.Draw(passEncoder, wireframe);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            _model?.Dispose();
        }
    }
}
