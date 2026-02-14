using System.Runtime.InteropServices;
using Solaris.Builders;
using Solaris.RHI;

namespace XNOEdit.Renderer
{
    public abstract class Shader : IDisposable
    {
        protected readonly SlDevice Device;
        private readonly SlShaderModule _shaderModule;

        private readonly List<SlBindGroupLayout> _bindGroupLayouts = [];
        private readonly List<SlBindGroup> _bindGroups = [];

        public int BindGroupCount => _bindGroups.Count;

        private readonly Dictionary<string, SlRenderPipeline> _pipelines;
        private readonly SlVertexBufferLayout[] _vertexLayouts;
        protected GCHandle Attributes;

        private readonly List<IDisposable> _resources;
        private bool _disposed;

        protected Shader(
            SlDevice device,
            string shaderSource,
            string label,
            Dictionary<string, SlPipelineVariantDescriptor> pipelineVariants)
        {
            Device = device;
            _resources = [];
            _pipelines = new Dictionary<string, SlRenderPipeline>();

            _shaderModule = CreateShaderModule(device, shaderSource, label);
            _vertexLayouts = CreateVertexLayouts();

            // Let derived classes set up their bind groups
            SetupBindGroups();

            // Create pipelines with all registered layouts
            foreach (var (name, descriptor) in pipelineVariants)
            {
                var pipeline = CreatePipeline(descriptor);
                _pipelines[name] = pipeline;
            }
        }

        protected abstract SlVertexBufferLayout[] CreateVertexLayouts();

        /// <summary>
        /// Derived classes override this to create their bind group layouts and bind groups.
        /// Call RegisterBindGroup() for each group in order (0, 1, 2, etc.)
        /// </summary>
        protected abstract void SetupBindGroups();

        /// <summary>
        /// Register a bind group layout and its corresponding bind group.
        /// Must be called in order: group 0, then 1, then 2, etc.
        /// </summary>
        protected void RegisterBindGroup(SlBindGroupLayout layout, SlBindGroup bindGroup)
        {
            _bindGroupLayouts.Add(layout);
            _bindGroups.Add(bindGroup);
        }

        public SlBindGroupLayout GetBindGroupLayout(int index)
        {
            if (index < 0 || index >= _bindGroupLayouts.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _bindGroupLayouts[index];
        }

        /// <summary>
        /// Get a specific bind group by index
        /// </summary>
        public SlBindGroup GetBindGroup(int index = 0)
        {
            if (index < 0 || index >= _bindGroups.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _bindGroups[index];
        }

        public SlRenderPipeline GetPipeline(string variant = "default")
        {
            if (_pipelines.TryGetValue(variant, out var pipeline))
                return pipeline;

            throw new KeyNotFoundException($"Pipeline variant '{variant}' not found");
        }

        private SlRenderPipeline CreatePipeline(SlPipelineVariantDescriptor descriptor)
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

        private static SlShaderModule CreateShaderModule(SlDevice device, string source, string label)
        {
            var descriptor = new SlShaderModuleDescriptor
            {
                Label = label,
                Source = source,
                Language = SlShaderLanguage.Wgsl
            };

            return device.CreateShaderModule(descriptor);
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

            foreach (var pipeline in _pipelines.Values)
            {
                pipeline.Dispose();
            }

            if (Attributes.IsAllocated)
                Attributes.Free();

            // Release all bind groups and layouts
            foreach (var bindGroup in _bindGroups)
            {
                bindGroup.Dispose();
            }

            foreach (var layout in _bindGroupLayouts)
            {
                layout.Dispose();
            }

            _shaderModule.Dispose();

            _disposed = true;
        }
    }

    public unsafe class Shader<TUniforms> : Shader where TUniforms : unmanaged
    {
        private SlBuffer<TUniforms> _uniformBuffer;

        protected Shader(
            SlDevice device,
            string shaderSource,
            string label,
            Dictionary<string, SlPipelineVariantDescriptor> pipelineVariants)
            : base(device, shaderSource, label, pipelineVariants)
        {
        }

        protected override SlVertexBufferLayout[] CreateVertexLayouts()
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
