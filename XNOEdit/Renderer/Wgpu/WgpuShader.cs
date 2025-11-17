using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using XNOEdit.Renderer.Builders;

namespace XNOEdit.Renderer.Wgpu
{
    public abstract unsafe class WgpuShader : IDisposable
    {
        protected readonly WebGPU Wgpu;
        protected readonly Device* Device;
        protected readonly Queue* Queue;
        private readonly ShaderModule* _shaderModule;
        private readonly BindGroupLayout* _bindGroupLayout;
        private readonly BindGroup* _bindGroup;
        private readonly Dictionary<string, IntPtr> _pipelines;
        private readonly VertexBufferLayout[] _vertexLayouts;
        protected GCHandle Attributes;

        private readonly List<IDisposable> _resources;
        private bool _disposed;

        public BindGroup* BindGroup => _bindGroup;

        protected WgpuShader(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            string shaderSource,
            string label,
            TextureFormat colorFormat,
            Dictionary<string, PipelineVariantDescriptor> pipelineVariants)
        {
            Wgpu = wgpu;
            Device = device;
            Queue = queue;
            _resources = [];
            _pipelines = new Dictionary<string, IntPtr>();

            _shaderModule = CreateShaderModule(wgpu, device, shaderSource, label);
            _vertexLayouts = CreateVertexLayouts();

            var (bindGroupLayout, bindGroup) = CreateBindGroupResources();
            _bindGroupLayout = (BindGroupLayout*)bindGroupLayout;
            _bindGroup = (BindGroup*)bindGroup;

            foreach (var (name, descriptor) in pipelineVariants)
            {
                var pipeline = CreatePipeline(colorFormat, descriptor);
                _pipelines[name] = (IntPtr)pipeline;
            }
        }

        protected abstract VertexBufferLayout[] CreateVertexLayouts();

        protected abstract (IntPtr, IntPtr) CreateBindGroupResources();

        public RenderPipeline* GetPipeline(string variant = "default")
        {
            if (_pipelines.TryGetValue(variant, out var pipeline))
                return (RenderPipeline*)pipeline;

            throw new KeyNotFoundException($"Pipeline variant '{variant}' not found");
        }

        private RenderPipeline* CreatePipeline(TextureFormat colorFormat, PipelineVariantDescriptor descriptor)
        {
            var builder = new RenderPipelineBuilder(Wgpu, Device, colorFormat)
                .WithShader(_shaderModule)
                .WithBindGroupLayout(_bindGroupLayout)
                .WithVertexLayouts(_vertexLayouts)
                .WithTopology(descriptor.Topology)
                .WithCulling(descriptor.CullMode, descriptor.FrontFace)
                .WithDepth(TextureFormat.Depth24Plus, descriptor.DepthWrite, descriptor.DepthCompare);

            if (descriptor.AlphaBlend)
                builder.WithAlphaBlend();

            return builder;
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
                    Chain = new ChainedStruct { SType = SType.ShaderModuleWgslDescriptor },
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

        protected void RegisterResource(IDisposable resource)
        {
            if (resource != null)
                _resources.Add(resource);
        }

        public virtual void Dispose()
        {
            if (_disposed) return;

            foreach (var resource in _resources)
            {
                try { resource?.Dispose(); }
                catch { /* Swallow */ }
            }

            foreach (var pipeline in _pipelines.Values)
            {
                if (pipeline != IntPtr.Zero)
                    Wgpu.RenderPipelineRelease((RenderPipeline*)pipeline);
            }

            if (Attributes.IsAllocated)
                Attributes.Free();

            if (_bindGroup != null)
                Wgpu.BindGroupRelease(_bindGroup);

            if (_bindGroupLayout != null)
                Wgpu.BindGroupLayoutRelease(_bindGroupLayout);

            if (_shaderModule != null)
                Wgpu.ShaderModuleRelease(_shaderModule);

            _disposed = true;
        }
    }

    public unsafe class WgpuShader<TUniforms> : WgpuShader where TUniforms : unmanaged
    {
        private WgpuBuffer<TUniforms> _uniformBuffer;

        protected WgpuShader(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            string shaderSource,
            string label,
            TextureFormat colorFormat,
            Dictionary<string, PipelineVariantDescriptor> pipelineVariants)
            : base(wgpu, device, queue, shaderSource, label, colorFormat, pipelineVariants)
        {
        }

        protected override VertexBufferLayout[] CreateVertexLayouts()
        {
            throw new NotImplementedException();
        }

        protected override (IntPtr, IntPtr) CreateBindGroupResources()
        {
            _uniformBuffer = WgpuBuffer<TUniforms>.CreateUniform(Wgpu, Device);
            RegisterResource(_uniformBuffer);

            var bindGroupBuilder = new BindGroupBuilder(Wgpu, Device);
            bindGroupBuilder.AddUniformBuffer(0, _uniformBuffer);

            var layout = bindGroupBuilder.BuildLayout();
            var bindGroup = bindGroupBuilder.BuildBindGroup();

            return ((IntPtr)layout, (IntPtr)bindGroup);
        }

        public void UpdateUniforms(in TUniforms uniforms)
        {
            _uniformBuffer.UpdateData(Queue, in uniforms);
        }
    }
}
