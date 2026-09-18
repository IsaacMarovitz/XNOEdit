using System.Numerics;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Solaris;
using Solaris.Graph;
using XNOEdit.Managers;
using XNOEdit.Renderer.Shaders;

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
        public TextureManager TextureManager;
    }

    public class ModelRenderer : Renderer<ModelParameters>
    {
        private readonly Model _model;

        public ModelRenderer(
            SlDevice device,
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk,
            ArcFile shaderArchive)
            : base(CreateShader(device))
        {
            _model = new Model(device, objectChunk, textureListChunk, effectListChunk, shaderArchive);
        }

        public static ModelShader CreateShader(SlDevice device)
        {
            return new ModelShader(device, ShaderLibrary.Get(device, "model_vs"),
                ShaderLibrary.Get(device, "model_ps"));
        }

        public bool GetVisible() => _model.GetAnyMeshVisible();
        public void SetVisible(bool visible) => _model.SetAllVisible(visible);
        public bool GetSubobjectVisible(int subobject) => _model.GetSubobjectVisible(subobject);
        public bool GetMeshSetVisible(int subobject, int meshSet) => _model.GetMeshSetVisible(subobject, meshSet);

        public void SetVisible(int subobject, int? meshSet, bool visibility)
        {
            _model.SetVisible(subobject, meshSet, visibility);
        }

        public override void Draw(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            ModelParameters modelParameters)
        {
            var perFrame = new PerFrameUniforms
            {
                Model = Matrix4x4.Identity,
                View = view,
                Projection = projection,
                SunDirection = modelParameters.SunDirection.AsVector4(),
                SunColor = modelParameters.SunColor.AsVector4(),
                CameraPosition = modelParameters.Position,
                VertColorStrength = modelParameters.VertColorStrength,
                Lightmap = modelParameters.Lightmap ? 1.0f : 0.0f,
            };

            var push = new ModelPerFramePush
            {
                PerFrameOffset = ctx.UploadConstants(in perFrame),
                SamplerIndex = ((ModelShader)ShaderModule).Sampler.Slot
            };
            ctx.PushConstants(in push);

            var variant = modelParameters.CullBackfaces ? "culled" : "default";
            ctx.SetPipeline(Material.Pipeline(variant, ctx.Signature));

            _model.Draw(ctx, modelParameters.TextureManager);
        }

        public override void Dispose()
        {
            _model.Dispose();

            base.Dispose();
        }
    }
}
