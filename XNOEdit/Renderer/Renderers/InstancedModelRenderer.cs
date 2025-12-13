using System.Numerics;
using System.Runtime.InteropServices;
using Marathon.Formats.Archive;
using Marathon.Formats.Ninja.Chunks;
using Silk.NET.WebGPU;
using XNOEdit.Managers;
using XNOEdit.Renderer.Shaders;
using XNOEdit.Renderer.Wgpu;

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

    public unsafe class InstancedModelRenderer : WgpuRenderer<InstancedModelParameters>
    {
        private readonly Device* _device;
        private readonly Model _model;

        private WgpuBuffer<InstanceData>? _instanceBuffer;
        private InstanceData[] _instances = [];

        public int InstanceCount => _instances.Length;

        public InstancedModelRenderer(
            WebGPU wgpu,
            WgpuDevice device,
            ObjectChunk objectChunk,
            TextureListChunk? textureListChunk,
            EffectListChunk? effectListChunk,
            ArcFile? shaderArchive)
            : base(wgpu, CreateShader(wgpu, device))
        {
            _device = device;
            _model = new Model(wgpu, device, objectChunk, textureListChunk, effectListChunk, shaderArchive, (InstancedModelShader)Shader);
        }

        private static InstancedModelShader CreateShader(WebGPU wgpu, WgpuDevice device)
        {
            return new InstancedModelShader(wgpu, device, EmbeddedResources.ReadAllText("XNOEdit/Shaders/InstancedModel.wgsl"));
        }

        public void SetInstances(InstanceData[] instances)
        {
            _instanceBuffer?.Dispose();
            _instances = instances;

            if (instances.Length == 0)
            {
                _instanceBuffer = null;
                return;
            }

            _instanceBuffer = new WgpuBuffer<InstanceData>(
                Wgpu,
                _device,
                instances,
                BufferUsage.Vertex | BufferUsage.CopyDst);
        }

        public void SetInstances(IEnumerable<(Vector3 position, Quaternion rotation)> transforms)
        {
            var instances = transforms
                .Select(t => InstanceData.Create(t.position, t.rotation))
                .ToArray();

            SetInstances(instances);
        }

        public void SetInstances(IEnumerable<(Vector3 position, Quaternion rotation, Vector3 scale)> transforms)
        {
            var instances = transforms
                .Select(t => InstanceData.Create(t.position, t.rotation, t.scale))
                .ToArray();

            SetInstances(instances);
        }

        public void UpdateInstance(int index, InstanceData data)
        {
            if (_instanceBuffer == null || index < 0 || index >= _instances.Length)
                return;

            _instances[index] = data;

            var queue = Wgpu.DeviceGetQueue(_device);
            _instanceBuffer.UpdateData(queue, index, in data);
            Wgpu.QueueRelease(queue);
        }

        public override void Draw(
            Queue* queue,
            RenderPassEncoder* passEncoder,
            Matrix4x4 view,
            Matrix4x4 projection,
            InstancedModelParameters parameters)
        {
            if (_instances.Length == 0 || _instanceBuffer == null)
                return;

            base.Draw(queue, passEncoder, view, projection, parameters);

            var shader = (InstancedModelShader)Shader;

            var perFrameUniforms = new PerFrameUniforms
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

            shader.UpdatePerFrameUniforms(queue, in perFrameUniforms);

            var pipeline = shader.GetPipeline(parameters.CullBackfaces, parameters.Wireframe);
            Wgpu.RenderPassEncoderSetPipeline(passEncoder, pipeline);

            // Bind instance buffer
            Wgpu.RenderPassEncoderSetVertexBuffer(
                passEncoder,
                1,
                _instanceBuffer.Handle,
                0,
                (ulong)(_instances.Length * sizeof(InstanceData)));

            _model.Draw(passEncoder, parameters.Wireframe, parameters.TextureManager, shader, _instances.Length);
        }

        public override void Dispose()
        {
            _instanceBuffer?.Dispose();
            _model.Dispose();
            base.Dispose();
        }
    }
}
