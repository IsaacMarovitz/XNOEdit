using System.Numerics;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Shaders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer.Renderers
{
    public struct ModelParameters
    {
        public Vector3 SunDirection;
        public Vector3 SunColor;
        public Vector3 Position;
        public float VertColorStrength;
        public bool Wireframe;
        public bool CullBackfaces;
    }

    public unsafe class ModelRenderer : WgpuRenderer<ModelParameters>
    {
        private readonly Model _model;

        public ModelRenderer(WebGPU wgpu, WgpuDevice device, Model model)
            : base(wgpu, CreateShader(wgpu, device))
        {
            _model = model;
        }

        private static ModelShader CreateShader(WebGPU wgpu, WgpuDevice device)
        {
            return new ModelShader(wgpu, device, EmbeddedResources.ReadAllText("XNOEdit/Shaders/BasicModel.wgsl"));
        }

        public override void Draw(
            Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters)
        {
            var uniforms = new BasicModelUniforms
            {
                Model = Matrix4x4.Identity,
                View = view,
                Projection = projection,
                SunDirection = modelParameters.SunDirection.AsVector4(),
                SunColor = modelParameters.SunColor.AsVector4(),
                Position = modelParameters.Position,
                VertColorStrength = modelParameters.VertColorStrength
            };

            ((ModelShader)Shader).UpdateUniforms(queue, in uniforms);

            var pipeline = ((ModelShader)Shader).GetPipeline(modelParameters.CullBackfaces, modelParameters.Wireframe);
            Wgpu.RenderPassEncoderSetPipeline(passEncoder, pipeline);

            uint dynamicOffset = 0;
            Wgpu.RenderPassEncoderSetBindGroup(passEncoder, 0, Shader.BindGroup, 0, &dynamicOffset);

            _model.Draw(passEncoder, modelParameters.Wireframe);
        }

        public override void Dispose()
        {
            _model?.Dispose();

            base.Dispose();
        }
    }
}
