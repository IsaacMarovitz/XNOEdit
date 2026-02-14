using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Solaris.Builders;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    public abstract unsafe class WgpuShader : IDisposable
    {
        protected readonly SlDevice Device;
        private readonly ShaderModule* _shaderModule;

        private readonly List<IntPtr> _bindGroupLayouts = [];
        private readonly List<IntPtr> _bindGroups = [];

        public int BindGroupCount => _bindGroups.Count;

        private readonly Dictionary<string, IntPtr> _pipelines;
        private readonly VertexBufferLayout[] _vertexLayouts;
        protected GCHandle Attributes;

        private readonly List<IDisposable> _resources;
        private bool _disposed;

        protected WgpuShader(
            SlDevice device,
            string shaderSource,
            string label,
            Dictionary<string, SlPipelineVariantDescriptor> pipelineVariants)
        {
            Device = device;
            _resources = [];
            _pipelines = new Dictionary<string, IntPtr>();

            _shaderModule = CreateShaderModule(device, shaderSource, label);
            _vertexLayouts = CreateVertexLayouts();

            // Let derived classes set up their bind groups
            SetupBindGroups();

            // Create pipelines with all registered layouts
            foreach (var (name, descriptor) in pipelineVariants)
            {
                var pipeline = CreatePipeline(descriptor);
                _pipelines[name] = (IntPtr)pipeline;
            }
        }

        protected abstract VertexBufferLayout[] CreateVertexLayouts();

        /// <summary>
        /// Derived classes override this to create their bind group layouts and bind groups.
        /// Call RegisterBindGroup() for each group in order (0, 1, 2, etc.)
        /// </summary>
        protected abstract void SetupBindGroups();

        /// <summary>
        /// Register a bind group layout and its corresponding bind group.
        /// Must be called in order: group 0, then 1, then 2, etc.
        /// </summary>
        protected void RegisterBindGroup(BindGroupLayout* layout, BindGroup* bindGroup)
        {
            _bindGroupLayouts.Add((IntPtr)layout);
            _bindGroups.Add((IntPtr)bindGroup);
        }

        public BindGroupLayout* GetBindGroupLayout(int index)
        {
            if (index < 0 || index >= _bindGroupLayouts.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return (BindGroupLayout*)_bindGroupLayouts[index];
        }

        /// <summary>
        /// Get a specific bind group by index
        /// </summary>
        public BindGroup* GetBindGroup(int index = 0)
        {
            if (index < 0 || index >= _bindGroups.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return (BindGroup*)_bindGroups[index];
        }

        public RenderPipeline* GetPipeline(string variant = "default")
        {
            if (_pipelines.TryGetValue(variant, out var pipeline))
                return (RenderPipeline*)pipeline;

            throw new KeyNotFoundException($"Pipeline variant '{variant}' not found");
        }

        private RenderPipeline* CreatePipeline(SlPipelineVariantDescriptor descriptor)
        {
            var builder = new RenderPipelineBuilder(Device)
                .WithShader(_shaderModule)
                .WithBindGroupLayouts(_bindGroupLayouts.ToArray())
                .WithVertexLayouts(_vertexLayouts)
                .WithTopology(descriptor.Topology)
                .WithCulling(descriptor.CullMode, descriptor.FrontFace)
                .WithDepth(descriptor.DepthWrite, descriptor.DepthCompare);

            if (descriptor.AlphaBlend)
                builder.WithAlphaBlend();

            return builder;
        }

        private static ShaderModule* CreateShaderModule(SlDevice device, string source, string label)
        {
            var src = SilkMarshal.StringToPtr(source);
            var shaderName = SilkMarshal.StringToPtr(label);
            var wgpuDevice = device as WgpuDevice;

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

                return wgpuDevice.Wgpu.DeviceCreateShaderModule(wgpuDevice, &descriptor);
            }
            finally
            {
                SilkMarshal.Free(src);
                if (shaderName != 0)
                    SilkMarshal.Free(shaderName);
            }
        }

        protected void RegisterResource(IDisposable? resource)
        {
            if (resource != null)
                _resources.Add(resource);
        }

        public virtual void Dispose()
        {
            if (_disposed) return;

            foreach (var resource in _resources)
            {
                try { resource.Dispose(); }
                catch { /* Swallow */ }
            }

            // TODO: Clean this up
            var wgpu = (Device as WgpuDevice).Wgpu;

            foreach (var pipeline in _pipelines.Values)
            {
                if (pipeline != IntPtr.Zero)
                    wgpu.RenderPipelineRelease((RenderPipeline*)pipeline);
            }

            if (Attributes.IsAllocated)
                Attributes.Free();

            // Release all bind groups and layouts
            foreach (BindGroup* bindGroup in _bindGroups)
            {
                if ((IntPtr)bindGroup != IntPtr.Zero)
                    wgpu.BindGroupRelease(bindGroup);
            }

            foreach (BindGroupLayout* layout in _bindGroupLayouts)
            {
                if ((IntPtr)layout != IntPtr.Zero)
                    wgpu.BindGroupLayoutRelease(layout);
            }

            if (_shaderModule != null)
                wgpu.ShaderModuleRelease(_shaderModule);

            _disposed = true;
        }
    }

    public unsafe class WgpuShader<TUniforms> : WgpuShader where TUniforms : unmanaged
    {
        private SlBuffer<TUniforms> _uniformBuffer;

        protected WgpuShader(
            SlDevice device,
            string shaderSource,
            string label,
            Dictionary<string, SlPipelineVariantDescriptor> pipelineVariants)
            : base(device, shaderSource, label, pipelineVariants)
        {
        }

        protected override VertexBufferLayout[] CreateVertexLayouts()
        {
            throw new NotImplementedException();
        }

        protected override void SetupBindGroups()
        {
            // Default: Create bind group 0 for uniforms
            _uniformBuffer = Device.CreateUniform<TUniforms>();
            RegisterResource(_uniformBuffer);

            var bindGroupBuilder = new BindGroupBuilder(Device);
            bindGroupBuilder.AddUniformBuffer(0, _uniformBuffer);

            var layout = bindGroupBuilder.BuildLayout();
            var bindGroup = bindGroupBuilder.BuildBindGroup();

            RegisterBindGroup(layout, bindGroup);
        }

        public void UpdateUniforms(SlQueue queue, in TUniforms uniforms)
        {
            _uniformBuffer.UpdateData(queue, in uniforms);
        }
    }
}
