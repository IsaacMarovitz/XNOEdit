using System.Runtime.InteropServices;
using Plume;

namespace Solaris
{
    public enum SlBackend
    {
        Vulkan,
        D3D12,
        Metal,
    }

    public readonly struct SlDeviceCapabilities
    {
        public required bool Bindless { get; init; }
        public required bool PresentWait { get; init; }
        public required bool TriangleFan { get; init; }
        public required bool DynamicDepthBias { get; init; }
        public required bool UnifiedMemory { get; init; }
        public required ulong MaxTextureSize { get; init; }
        public required RenderShaderFormat ShaderFormat { get; init; }
    }

    public sealed unsafe class SlDevice : IDisposable
    {
        public const int FramesInFlight = 2;

        private RenderInterface* _interface;
        private RenderDevice* _device;
        private RenderCommandQueue* _queue;
        private bool _disposed;

        private SlDevice(RenderInterface* renderInterface, RenderDevice* device, RenderCommandQueue* queue, SlBackend backend)
        {
            _interface = renderInterface;
            _device = device;
            _queue = queue;
            Backend = backend;

            Retirement = new SlRetirementQueue(FramesInFlight);

            var deviceCaps = device->GetCapabilities();
            var interfaceCaps = renderInterface->GetCapabilities();

            Capabilities = new SlDeviceCapabilities
            {
                Bindless = deviceCaps->DescriptorIndexing,
                PresentWait = deviceCaps->PresentWait,
                TriangleFan = deviceCaps->TriangleFan,
                DynamicDepthBias = deviceCaps->DynamicDepthBias,
                UnifiedMemory = deviceCaps->Uma,
                MaxTextureSize = deviceCaps->MaxTextureSize,
                ShaderFormat = interfaceCaps->ShaderFormat,
            };

            if (!Capabilities.Bindless)
            {
                throw new PlatformNotSupportedException(
                    "Solaris requires bindless descriptor indexing, which this device does not report.");
            }

            Layout = new SlGlobalLayout(_device, 1024, 64);
            Tables = new SlBindlessTables(_device, Layout, Retirement);
        }

        public SlBackend Backend { get; }

        public SlDeviceCapabilities Capabilities { get; }

        public SlBindlessTables Tables { get; }

        public SlGlobalLayout Layout { get; }

        public SlRetirementQueue Retirement { get; }

        internal RenderDevice* Handle => _device;

        internal RenderCommandQueue* Queue => _queue;

        public string Name
        {
            get
            {
                var description = _device->GetDescription();
                return description->GetName();
            }
        }

        public static SlDevice Create(SlBackend? preferred = null)
        {
            var backend = preferred ?? DefaultBackend();

            var renderInterface = backend switch
            {
                SlBackend.Metal => GlobalMethods.CreateMetalInterface(),
                SlBackend.D3D12 => GlobalMethods.CreateD3D12Interface(),
                SlBackend.Vulkan => GlobalMethods.CreateVulkanInterface(),
                _ => throw new ArgumentOutOfRangeException(nameof(preferred), backend, null),
            };

            if (renderInterface == null)
                throw new InvalidOperationException($"Failed to create the {backend} render interface.");

            var name = Marshal.StringToCoTaskMemUTF8("");
            var device = renderInterface->CreateDevice((sbyte*)name);
            Marshal.ZeroFreeCoTaskMemUTF8(name);

            if (device == null)
            {
                renderInterface->Dispose();
                throw new InvalidOperationException($"The {backend} interface reported no usable device.");
            }

            var queue = device->CreateCommandQueue(RenderCommandListType.Direct);

            if (queue == null)
            {
                device->Dispose();
                renderInterface->Dispose();
                throw new InvalidOperationException("Failed to create the graphics command queue.");
            }

            return new SlDevice(renderInterface, device, queue, backend);
        }

        private static SlBackend DefaultBackend()
        {
            if (OperatingSystem.IsMacOS())
                return SlBackend.Metal;

            if (OperatingSystem.IsWindows())
                return SlBackend.D3D12;

            return SlBackend.Vulkan;
        }

        public SlBuffer CreateBuffer(ulong sizeInBytes, SlBufferUsage usage, string? name = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var desc = new RenderBufferDesc
            {
                Size = sizeInBytes,
                HeapType = (usage & SlBufferUsage.Upload) != 0 ? RenderHeapType.Upload : RenderHeapType.Default,
                Flags = ToPlumeBufferFlags(usage),
            };

            var handle = _device->CreateBuffer(&desc);

            if (handle == null)
                throw new InvalidOperationException($"Failed to create a {sizeInBytes} byte buffer.");

            var buffer = new SlBuffer(handle, sizeInBytes, usage);

            if (name != null)
            {
                buffer.SetName(name);
            }

            return buffer;
        }

        /// <summary>
        /// Creates an upload buffer already populated from <paramref name="data"/>.
        /// Device-local buffers need a staging copy through a command list, which the
        /// frame graph exposes instead.
        /// </summary>
        public SlBuffer CreateBuffer<T>(ReadOnlySpan<T> data, SlBufferUsage usage, string? name = null) where T : unmanaged
        {
            var sizeInBytes = (ulong)data.Length * (ulong)System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            var buffer = CreateBuffer(sizeInBytes, usage | SlBufferUsage.Upload, name);
            buffer.Write(data);
            return buffer;
        }

        public SlTexture CreateTexture(in SlTextureDescriptor descriptor, string? name = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var multisampling = new RenderMultisampling((RenderSampleCounts)descriptor.SampleCount);

            var desc = new RenderTextureDesc
            {
                Dimension = descriptor.Dimension,
                Width = descriptor.Width,
                Height = descriptor.Height,
                Depth = descriptor.Depth,
                MipLevels = descriptor.MipLevels,
                ArraySize = descriptor.ArraySize,
                Format = descriptor.Format,
                Multisampling = multisampling,
                Flags = descriptor.ToPlumeFlags(),
                Committed = (descriptor.Usage & (SlTextureUsage.ColorTarget | SlTextureUsage.DepthTarget)) != 0,
            };

            var handle = _device->CreateTexture(&desc);

            if (handle == null)
                throw new InvalidOperationException($"Failed to create a {descriptor.Width}x{descriptor.Height} texture.");

            var texture = new SlTexture(handle, descriptor, ownsTexture: true);

            if (name != null)
            {
                texture.SetName(name);
            }

            return texture;
        }

        public SlSamplerIndex GetSampler(in SlSamplerDescriptor descriptor) => Tables.Register(descriptor);

        /// <summary>Queues a resource for destruction once in-flight frames complete.</summary>
        public void Retire(IDisposable resource) => Retirement.Retire(resource);

        public uint GetSampleCountsSupported(RenderFormat format) => _device->GetSampleCountsSupported(format);

        private static RenderBufferFlags ToPlumeBufferFlags(SlBufferUsage usage)
        {
            var flags = RenderBufferFlags.None;

            if ((usage & SlBufferUsage.Vertex) != 0)
                flags |= RenderBufferFlags.Vertex;

            if ((usage & SlBufferUsage.Index) != 0)
                flags |= RenderBufferFlags.Index;

            if ((usage & SlBufferUsage.Constant) != 0)
                flags |= RenderBufferFlags.Constant;

            if ((usage & SlBufferUsage.Structured) != 0)
                flags |= RenderBufferFlags.Storage;

            if ((usage & SlBufferUsage.DeviceAddressable) != 0)
                flags |= RenderBufferFlags.DeviceAddressable;

            return flags;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            Retirement.Flush();
            Tables.Dispose();
            Layout.Dispose();

            if (_queue != null)
            {
                _queue->Dispose();
                _queue = null;
            }

            if (_device != null)
            {
                _device->Dispose();
                _device = null;
            }

            if (_interface != null)
            {
                _interface->Dispose();
                _interface = null;
            }
        }
    }
}
