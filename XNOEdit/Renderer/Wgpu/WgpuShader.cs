using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Builders;

namespace XNOEdit.Renderer.Wgpu
{
    public unsafe class WgpuShader<TUniforms> : IDisposable where TUniforms : unmanaged
    {
        private readonly WebGPU _wgpu;
        private readonly Queue* _queue;
        private readonly WgpuBuffer<TUniforms> _uniformBuffer;
        private readonly BindGroup* _bindGroup;

        protected readonly ShaderModule* ShaderModule;
        protected readonly BindGroupLayout* BindGroupLayout;
        protected RenderPipeline* Pipeline;

        public BindGroup* UniformBindGroup => _bindGroup;
        public RenderPipeline* GetPipeline() => Pipeline;

        public WgpuShader(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            string shaderSource,
            string label,
            TextureFormat colorFormat,
            VertexBufferLayout[] vertexLayouts,
            RenderPipelineBuilder pipelineBuilder = null)
        {
            _wgpu = wgpu;
            _queue = queue;

            ShaderModule = CreateShaderModule(wgpu, device, shaderSource, label);

            _uniformBuffer = WgpuBuffer<TUniforms>.CreateUniform(wgpu, device);

            var bindGroupBuilder = new BindGroupBuilder(wgpu, device);
            bindGroupBuilder.AddUniformBuffer(0, _uniformBuffer);
            BindGroupLayout = bindGroupBuilder.BuildLayout();
            _bindGroup = bindGroupBuilder.BuildBindGroup();

            if (pipelineBuilder != null)
            {
                Pipeline = pipelineBuilder
                    .WithShader(ShaderModule)
                    .WithBindGroupLayout(BindGroupLayout)
                    .WithVertexLayouts(vertexLayouts)
                    .Build();
            }
            else
            {
                Pipeline = new RenderPipelineBuilder(wgpu, device, colorFormat)
                    .WithShader(ShaderModule)
                    .WithBindGroupLayout(BindGroupLayout)
                    .WithVertexLayouts(vertexLayouts)
                    .WithDepth()
                    .WithAlphaBlend()
                    .Build();
            }
        }

        public void UpdateUniforms(in TUniforms uniforms)
        {
            _uniformBuffer.UpdateData(_queue, in uniforms);
        }

        public virtual void Dispose()
        {
            _uniformBuffer?.Dispose();

            if (_bindGroup != null)
                _wgpu.BindGroupRelease(_bindGroup);

            if (BindGroupLayout != null)
                _wgpu.BindGroupLayoutRelease(BindGroupLayout);

            if (Pipeline != null)
                _wgpu.RenderPipelineRelease(Pipeline);

            if (ShaderModule != null)
                _wgpu.ShaderModuleRelease(ShaderModule);
        }

        private static ShaderModule* CreateShaderModule(
            WebGPU wgpu,
            Device* device,
            string source,
            string label = null)
        {
            var src = SilkMarshal.StringToPtr(source);
            var shaderName = label != null ? SilkMarshal.StringToPtr(label) : (nint)null;

            try
            {
                var wgslDescriptor = new ShaderModuleWGSLDescriptor
                {
                    Chain = new ChainedStruct
                    {
                        SType = SType.ShaderModuleWgslDescriptor
                    },
                    Code = (byte*)src
                };

                var descriptor = new ShaderModuleDescriptor
                {
                    Label = (byte*)shaderName,
                    NextInChain = (ChainedStruct*)&wgslDescriptor
                };

                return wgpu.DeviceCreateShaderModule(device, &descriptor);
            }
            finally
            {
                SilkMarshal.Free(src);

                if (shaderName != 0)
                    SilkMarshal.Free(shaderName);
            }
        }
    }
}
