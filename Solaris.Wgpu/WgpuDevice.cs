using System.Runtime.InteropServices;
using SDL3;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using XNOEdit.Logging;

namespace Solaris.Wgpu
{
    public unsafe class WgpuDevice : SlDevice
    {
        private readonly WgpuSurface _surface;
        private readonly Instance* _instance;
        private readonly Adapter* _adapter;
        private readonly Device* _handle;

        public readonly WebGPU Wgpu;

        public static implicit operator Device*(WgpuDevice device) => device._handle;

        private const int CopyBufferAlignment = 4;

        public WgpuDevice(WebGPU wgpu, IntPtr window)
        {
            Wgpu = wgpu;

            var extras = new InstanceExtras
            {
                Chain = new ChainedStruct { SType = (SType)NativeSType.STypeInstanceExtras },
                Backends = InstanceBackend.Metal,
            };

            var instanceDesc = new InstanceDescriptor
            {
                // NextInChain = (ChainedStruct*)&extras
            };

            _instance = Wgpu.CreateInstance(&instanceDesc);
            _surface = CreateWebGpuSurface(window, Wgpu, _instance);

            var adapterOptions = new RequestAdapterOptions
            {
                PowerPreference = PowerPreference.HighPerformance,
                CompatibleSurface = _surface
            };

            Adapter* adapter = null;
            Wgpu.InstanceRequestAdapter(
                _instance,
                &adapterOptions,
                new PfnRequestAdapterCallback((status, adapterPtr, message, userdata) =>
                {
                    if (status == RequestAdapterStatus.Success)
                    {
                        adapter = adapterPtr;
                    }
                }),
                null);

            _adapter = adapter;

            AdapterProperties adapterProps = default;
            Wgpu.AdapterGetProperties(_adapter, &adapterProps);
            Logger.Info?.PrintMsg(LogClass.Application, $"WGPU Backend: {adapterProps.BackendType}");

            NativeFeature[] feature = [NativeFeature.BufferBindingArray];

            fixed (NativeFeature* pFeature = feature)
            {
                var deviceDesc = new DeviceDescriptor();
                // var deviceDesc = new DeviceDescriptor
                // {
                //     RequiredFeatures = (FeatureName*)pFeature,
                //     RequiredFeatureCount = (uint)feature.Length
                // };

                Device* device = null;

                Wgpu.AdapterRequestDevice(
                    _adapter,
                    &deviceDesc,
                    new PfnRequestDeviceCallback((status, devicePtr, message, userdata) =>
                    {
                        if (status == RequestDeviceStatus.Success)
                        {
                            device = devicePtr;
                        }
                    }),
                    null);

                _handle = device;
            }

            SDL.GetWindowSizeInPixels(window, out var width, out var height);

            var surfaceConfig = new SlSurfaceDescriptor
            {
                Format = SlTextureFormat.Bgra8Unorm,
                Usage = SlTextureUsage.RenderAttachment,
                Width = (uint)width,
                Height = (uint)height,
                PresentMode = SlPresentMode.Fifo
            };
            _surface.Configure(surfaceConfig);
        }

        public override SlSurface GetSurface() => _surface;

        public override SlCommandEncoder CreateCommandEncoder()
        {
            return new WgpuCommandEncoder(this);
        }

        public override SlShaderModule CreateShaderModule(SlShaderModuleDescriptor descriptor)
        {
            return new WgpuShaderModule(this, descriptor);
        }

        public override SlBindGroupLayout CreateBindGroupLayout(SlBindGroupLayoutDescriptor descriptor)
        {
            return new WgpuBindGroupLayout(this, descriptor);
        }

        public override SlBindGroup CreateBindGroup(SlBindGroupDescriptor descriptor)
        {
            return new WgpuBindGroup(this, descriptor);
        }

        public override SlRenderPipeline CreateRenderPipeline(SlRenderPipelineDescriptor descriptor)
        {
            return new WgpuRenderPipeline(this, descriptor);
        }

        public override SlQueue GetQueue()
        {
            return new WgpuQueue(Wgpu, Wgpu.DeviceGetQueue(_handle));
        }

        public override SlTexture CreateTexture(SlTextureDescriptor descriptor)
        {
            return new WgpuTexture(this, descriptor);
        }

        public override SlSampler CreateSampler(SlSamplerDescriptor descriptor)
        {
            return new WgpuSampler(this, descriptor);
        }

        public override SlBuffer<T> CreateBuffer<T>(Span<T> data, SlBufferUsage usage)
        {
            // Calculate size and align to COPY_BUFFER_ALIGNMENT
            var dataSize = (ulong)(data.Length * sizeof(T));
            var size = AlignUp(dataSize, CopyBufferAlignment);

            var descriptor = new BufferDescriptor
            {
                Size = size,
                Usage = usage.Convert(),
                MappedAtCreation = true
            };

            var handle = Wgpu.DeviceCreateBuffer(_handle, &descriptor);

            // Write initial data
            var mappedRange = Wgpu.BufferGetMappedRange(handle, 0, (nuint)size);
            var dataBytes = MemoryMarshal.AsBytes(data);
            dataBytes.CopyTo(new Span<byte>(mappedRange, (int)dataSize));
            Wgpu.BufferUnmap(handle);

            return new WgpuBuffer<T>(handle, size, Wgpu);
        }

        public override SlBuffer<T> CreateBuffer<T>(SlBufferDescriptor descriptor)
        {
            var dataSize = descriptor.Size == 0 ? (ulong)sizeof(T) : descriptor.Size;
            var alignment = descriptor.Alignment > 0 ? descriptor.Alignment : CopyBufferAlignment;
            var size = AlignUp(dataSize, alignment);

            var wgpuDescriptor = new BufferDescriptor
            {
                Size = size,
                Usage = descriptor.Usage.Convert(),
                MappedAtCreation = false
            };

            var handle = Wgpu.DeviceCreateBuffer(_handle, &wgpuDescriptor);

            return new WgpuBuffer<T>(handle, size, Wgpu);
        }

        public override void Dispose()
        {
            _surface?.Dispose();

            if (_handle != null)
                Wgpu.DeviceRelease(_handle);

            if (_adapter != null)
                Wgpu.AdapterRelease(_adapter);

            if (_instance != null)
                Wgpu.InstanceRelease(_instance);
        }

        private WgpuSurface CreateWebGpuSurface(IntPtr window, WebGPU wgpu, Instance* instance)
        {
            var windowProperties = SDL.GetWindowProperties(window);
            var descriptor = new SurfaceDescriptor
            {
                NextInChain = (ChainedStruct*)IntPtr.Zero
            };

            var ptr = SDL.GetPointerProperty(windowProperties, SDL.Props.WindowX11DisplayPointer, IntPtr.Zero);
            if (ptr != IntPtr.Zero)
            {
                // Not sure if this is correct
                var windowNumber = SDL.GetNumberProperty(windowProperties, SDL.Props.WindowX11WindowNumber, 0);

                var xlibDescriptor = new SurfaceDescriptorFromXlibWindow
                {
                    Chain = new ChainedStruct
                    {
                        Next  = null,
                        SType = SType.SurfaceDescriptorFromXlibWindow
                    },
                    Display = (void*)ptr,
                    Window  = (ulong)windowNumber
                };

                descriptor.NextInChain = (ChainedStruct*)&xlibDescriptor;
            }

            ptr = SDL.GetPointerProperty(windowProperties, SDL.Props.WindowCocoaWindowPointer, IntPtr.Zero);
            if (ptr != IntPtr.Zero)
            {
                // Based on the Veldrid Metal bindings implementation:
                // https://github.com/veldrid/veldrid/tree/master/src/Veldrid.MetalBindings
                CAMetalLayer metalLayer = CAMetalLayer.New();
                NSWindow nsWindow = new(ptr);
                var contentView = nsWindow.contentView;
                contentView.wantsLayer = true;
                contentView.layer = metalLayer.NativePtr;

                var cocoaDescriptor = new SurfaceDescriptorFromMetalLayer
                {
                    Chain = new ChainedStruct
                    {
                        Next  = null,
                        SType = SType.SurfaceDescriptorFromMetalLayer
                    },
                    Layer = (void*)metalLayer.NativePtr
                };

                descriptor.NextInChain = (ChainedStruct*)&cocoaDescriptor;
            }

            ptr = SDL.GetPointerProperty(windowProperties, SDL.Props.WindowWaylandDisplayPointer, IntPtr.Zero);
            if (ptr != IntPtr.Zero)
            {
                var surfacePtr = SDL.GetPointerProperty(windowProperties, SDL.Props.WindowWaylandSurfacePointer, IntPtr.Zero);

                var waylandDescriptor = new SurfaceDescriptorFromWaylandSurface
                {
                    Chain = new ChainedStruct
                    {
                        Next  = null,
                        SType = SType.SurfaceDescriptorFromWaylandSurface
                    },
                    Display = (void*)ptr,
                    Surface = (void*)surfacePtr
                };

                descriptor.NextInChain = (ChainedStruct*)&waylandDescriptor;
            }

            ptr = SDL.GetPointerProperty(windowProperties, SDL.Props.WindowWin32HWNDPointer, IntPtr.Zero);
            if (ptr != IntPtr.Zero)
            {
                var instancePtr = SDL.GetPointerProperty(windowProperties, SDL.Props.WindowWin32InstancePointer, IntPtr.Zero);

                var win32Descriptor = new SurfaceDescriptorFromWindowsHWND
                {
                    Chain = new ChainedStruct
                    {
                        Next  = null,
                        SType = SType.SurfaceDescriptorFromWindowsHwnd
                    },
                    Hwnd      = (void*)ptr,
                    Hinstance = (void*)instancePtr
                };

                descriptor.NextInChain = (ChainedStruct*)&win32Descriptor;
            }

            if (descriptor.NextInChain == (void*)IntPtr.Zero)
            {
                throw new PlatformNotSupportedException("Cannot init WGPU surface for you platform.");
            }

            return new WgpuSurface(this, wgpu.InstanceCreateSurface(instance, descriptor));
        }

        private static ulong AlignUp(ulong value, int alignment)
        {
            return (value + (ulong)alignment - 1) & ~((ulong)alignment - 1);
        }
    }
}
