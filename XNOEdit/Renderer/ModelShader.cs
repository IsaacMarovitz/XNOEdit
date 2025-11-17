using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Builders;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Renderer
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BasicModelUniforms
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 LightDir;
        public Vector4 LightColor;
        public Vector3 ViewPos;
        public float VertColorStrength;
    }

    public unsafe class ModelShader : WgpuShader<BasicModelUniforms>, IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly RenderPipeline* _pipelineSolidNoCull;
        private readonly RenderPipeline* _pipelineSolidCull;
        private readonly RenderPipeline* _pipelineWireframe;

        public ModelShader(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            string shaderSource,
            string label,
            TextureFormat colorFormat,
            VertexBufferLayout[] vertexLayouts)
            : base(wgpu, device, queue, shaderSource, label, colorFormat, vertexLayouts, pipelineBuilder: null)
        {
            _wgpu = wgpu;

            var baseBuilder = new RenderPipelineBuilder(wgpu, device, colorFormat)
                .WithShader(ShaderModule)
                .WithBindGroupLayout(BindGroupLayout)
                .WithVertexLayouts(vertexLayouts)
                .WithDepth()
                .WithAlphaBlend();

            _pipelineSolidNoCull = baseBuilder
                .WithTopology(PrimitiveTopology.TriangleList)
                .WithCulling(CullMode.None, FrontFace.CW);

            _pipelineSolidCull = baseBuilder
                .WithTopology(PrimitiveTopology.TriangleList)
                .WithCulling(CullMode.Back, FrontFace.CW);

            _pipelineWireframe = baseBuilder
                .WithTopology(PrimitiveTopology.LineList)
                .WithCulling(CullMode.None);

            Pipeline = _pipelineSolidNoCull;
        }

        public RenderPipeline* GetPipeline(bool cullBackfaces, bool wireframe)
        {
            if (wireframe)
                return _pipelineWireframe;

            return cullBackfaces ? _pipelineSolidCull : _pipelineSolidNoCull;
        }

        public override void Dispose()
        {
            if (_pipelineSolidNoCull != null)
                _wgpu.RenderPipelineRelease(_pipelineSolidNoCull);

            if (_pipelineSolidCull != null)
                _wgpu.RenderPipelineRelease(_pipelineSolidCull);

            if (_pipelineWireframe != null)
                _wgpu.RenderPipelineRelease(_pipelineWireframe);

            base.Dispose();
        }
    }
}
