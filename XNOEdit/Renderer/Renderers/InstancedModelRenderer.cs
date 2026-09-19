using System.Numerics;
using System.Runtime.InteropServices;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Solaris;
using Solaris.Graph;
using XNOEdit.Managers;
using XNOEdit.Renderer.Shaders;

namespace XNOEdit.Renderer.Renderers
{
    [StructLayout(LayoutKind.Sequential)]
    public struct InstanceData
    {
        public Matrix4x4 Transform;

        public static InstanceData Create(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return new InstanceData
            {
                Transform = Matrix4x4.CreateScale(scale) *
                            Matrix4x4.CreateFromQuaternion(rotation) *
                            Matrix4x4.CreateTranslation(position)
            };
        }

        public static InstanceData Create(Vector3 position, Quaternion rotation)
            => Create(position, rotation, Vector3.One);

        public static InstanceData Create(Vector3 position)
            => Create(position, Quaternion.Identity, Vector3.One);
    }

    public struct InstancedModelParameters
    {
        public Vector3 SunDirection;
        public Vector3 SunColor;
        public Vector3 CameraPosition;
        public float VertColorStrength;
        public bool Wireframe;
        public bool CullBackfaces;
        public bool Lightmap;
        public TextureManager TextureManager;
    }

    public class InstancedModelRenderer : Renderer<InstancedModelParameters>
    {
        private readonly SlDevice _device;
        private readonly Model _model;

        private SlBuffer? _instanceBuffer;
        private InstanceData[] _instances = [];

        public int InstanceCount => _instances.Length;

        public InstancedModelRenderer(
            SlDevice device,
            ObjectChunk objectChunk,
            TextureListChunk textureListChunk,
            EffectListChunk effectListChunk,
            ArcFile shaderArchive)
            : base(CreateShader(device))
        {
            _device = device;
            _model = new Model(device, objectChunk, textureListChunk, effectListChunk, shaderArchive);
        }

        private static InstancedModelShader CreateShader(SlDevice device)
        {
            return new InstancedModelShader(device, ShaderLibrary.Get(device, "instanced_model_vs"),
                ShaderLibrary.Get(device, "model_ps"));
        }

        public void SetInstances(InstanceData[] instances)
        {
            if (_instanceBuffer != null)
                _device.Retire(_instanceBuffer);

            _instances = instances;

            if (instances.Length == 0)
            {
                _instanceBuffer = null;
                return;
            }

            _instanceBuffer = _device.CreateBuffer(instances, SlBufferUsage.Vertex);
        }

        public override void Draw(
            SlPassContext ctx,
            Matrix4x4 view,
            Matrix4x4 projection,
            InstancedModelParameters parameters)
        {
            if (_instances.Length == 0 || _instanceBuffer == null)
                return;

            var perFrame = new PerFrameUniforms
            {
                Model = Matrix4x4.Identity,
                View = view,
                Projection = projection,
                SunDirection = parameters.SunDirection.AsVector4(),
                SunColor = parameters.SunColor.AsVector4(),
                CameraPosition = parameters.CameraPosition,
                VertColorStrength = parameters.VertColorStrength,
                Lightmap = parameters.Lightmap ? 1.0f : 0.0f,
            };

            var perFrameOffset = ctx.UploadConstants(in perFrame);
            ctx.PushConstants(in perFrameOffset);

            var variant = parameters.CullBackfaces ? "culled" : "default";
            ctx.SetPipeline(Material.Pipeline(variant, ctx.Signature));
            ctx.SetVertexBuffer(1, _instanceBuffer.View, InstancedModelShader.InstanceStride);

            _model.Draw(ctx, parameters.TextureManager, _instances.Length);
        }

        public override void Dispose()
        {
            if (_instanceBuffer != null)
                _device.Retire(_instanceBuffer);

            _model.Dispose();
            base.Dispose();
        }
    }
}
