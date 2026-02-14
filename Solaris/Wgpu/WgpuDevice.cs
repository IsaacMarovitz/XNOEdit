using SDL3;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using XNOEdit.Logging;

namespace XNOEdit.Renderer.Wgpu
{
    public unsafe class WgpuDevice : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Surface* _surface;
        private readonly Instance* _instance;
        private readonly Adapter* _adapter;
        private readonly TextureFormat _surfaceFormat = TextureFormat.Bgra8Unorm;
        private Device* Device { get; }

        public static implicit operator Device*(WgpuDevice device) => device.Device;

        public WgpuDevice(WebGPU wgpu, IntPtr window)
        {
            _wgpu = wgpu;

            var extras = new InstanceExtras
            {
                Chain = new ChainedStruct { SType = (SType)NativeSType.STypeInstanceExtras },
                Backends = InstanceBackend.Metal,
            };

            var instanceDesc = new InstanceDescriptor
            {
                // NextInChain = (ChainedStruct*)&extras
            };

            _instance = _wgpu.CreateInstance(&instanceDesc);
            _surface = CreateWebGpuSurface(window, _wgpu, _instance);

            var adapterOptions = new RequestAdapterOptions
            {
                PowerPreference = PowerPreference.HighPerformance,
                CompatibleSurface = _surface
            };

            Adapter* adapter = null;
            _wgpu.InstanceRequestAdapter(
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
            _wgpu.AdapterGetProperties(_adapter, &adapterProps);
            Logger.Info?.PrintMsg(LogClass.Application, $"Backend type: {adapterProps.BackendType}");

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

                _wgpu.AdapterRequestDevice(
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

                Device = device;
            }

            SDL.GetWindowSizeInPixels(window, out var width, out var height);

            // Configure surface
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = Device,
                Format = _surfaceFormat,
                Usage = TextureUsage.RenderAttachment,
                Width = (uint)width,
                Height = (uint)height,
                PresentMode = PresentMode.Fifo,
                AlphaMode = CompositeAlphaMode.Auto
            };

            _wgpu.SurfaceConfigure(_surface, &surfaceConfig);
        }

        public Surface* GetSurface()
        {
            return _surface;
        }

        public TextureFormat GetSurfaceFormat()
        {
            return _surfaceFormat;
        }

        public void Dispose()
        {
            if (_surface != null)
                _wgpu.SurfaceRelease(_surface);

            if (Device != null)
                _wgpu.DeviceRelease(Device);

            if (_adapter != null)
                _wgpu.AdapterRelease(_adapter);

            if (_instance != null)
                _wgpu.InstanceRelease(_instance);
        }

        private static Surface* CreateWebGpuSurface(IntPtr window, WebGPU wgpu, Instance* instance)
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

            return wgpu.InstanceCreateSurface(instance, descriptor);
        }
    }
}
