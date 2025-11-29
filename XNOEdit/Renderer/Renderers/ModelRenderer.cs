using System.Numerics;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
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
        public bool Lightmap;
        public IReadOnlyDictionary<string, IntPtr> Textures;
    }

    public unsafe class ModelRenderer : WgpuRenderer<ModelParameters>
    {
        private readonly Model _model;
        private bool _visible;

        public ModelRenderer(
            WebGPU wgpu,
            WgpuDevice device,
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk,
            ArcFile shaderArchive)
            : base(wgpu, CreateShader(wgpu, device))
        {
            _model = new Model(wgpu, device, objectChunk, textureListChunk, effectListChunk, shaderArchive, (ModelShader)Shader);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
        }

        public void SetVisible(int subobject, int? meshSet, bool visibility)
        {
            _model.SetVisible(subobject, meshSet, visibility);
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
            if (!_visible) { return; }

            base.Draw(queue, passEncoder, view, projection, modelParameters);

            var modelShader = (ModelShader)Shader;

            var perFrameUniforms = new PerFrameUniforms
            {
                Model = Matrix4x4.Identity,
                View = view,
                Projection = projection,
                SunDirection = modelParameters.SunDirection.AsVector4(),
                SunColor = modelParameters.SunColor.AsVector4(),
                CameraPosition = modelParameters.Position,
                VertColorStrength = modelParameters.VertColorStrength,
                Lightmap = modelParameters.Lightmap ? 1.0f: 0.0f,
            };

            modelShader.UpdatePerFrameUniforms(queue, in perFrameUniforms);

            var pipeline = modelShader.GetPipeline(modelParameters.CullBackfaces, modelParameters.Wireframe);
            Wgpu.RenderPassEncoderSetPipeline(passEncoder, pipeline);

            _model.Draw(passEncoder, modelParameters.Wireframe, modelParameters.Textures, modelShader);
        }

        public override void Dispose()
        {
            _model?.Dispose();

            base.Dispose();
        }
    }
}
