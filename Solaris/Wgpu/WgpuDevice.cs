using System.Runtime.InteropServices;
using SDL3;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Solaris.RHI;
using XNOEdit.Logging;

namespace Solaris.Wgpu
{
    public unsafe class WgpuDevice : SlDevice
    {
        private readonly Surface* _surface;
        private readonly Instance* _instance;
        private readonly Adapter* _adapter;

        public readonly WebGPU Wgpu;
        private Device* Device { get; }

        public static implicit operator Device*(WgpuDevice device) => device.Device;

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

                Device = device;
            }

            SDL.GetWindowSizeInPixels(window, out var width, out var height);

            // Configure surface
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = Device,
                Format = TextureFormat.Bgra8Unorm,
                Usage = TextureUsage.RenderAttachment,
                Width = (uint)width,
                Height = (uint)height,
                PresentMode = PresentMode.Fifo,
                AlphaMode = CompositeAlphaMode.Auto
            };

            Wgpu.SurfaceConfigure(_surface, &surfaceConfig);
        }

        public Surface* GetSurface()
        {
            return _surface;
        }

        public override SlQueue GetQueue()
        {
            return new WgpuQueue(Wgpu, Wgpu.DeviceGetQueue(Device));
        }

        public override SlTexture CreateTexture(SlTextureDescriptor descriptor)
        {
            var wgpuDescriptor = new TextureDescriptor
            {
                Size = new Extent3D
                {
                    Width = descriptor.Size.Width,
                    Height = descriptor.Size.Height,
                    DepthOrArrayLayers = descriptor.Size.DepthOrArrayLayers
                },
                MipLevelCount = descriptor.MipLevelCount,
                SampleCount = descriptor.SampleCount,
                Dimension = descriptor.Dimension.Convert(),
                Format = descriptor.Format.Convert(),
                Usage = descriptor.Usage.Convert(),
            };

            var texture = Wgpu.DeviceCreateTexture(Device, &wgpuDescriptor);
            return new WgpuTexture(Wgpu, texture);
        }

        public override SlSampler CreateSampler(SlSamplerDescriptor descriptor)
        {
            var wgpuDescriptor = new SamplerDescriptor
            {
                AddressModeU = descriptor.AddressModeU.Convert(),
                AddressModeV = descriptor.AddressModeV.Convert(),
                AddressModeW = descriptor.AddressModeW.Convert(),
                MagFilter = descriptor.MagFilter.Convert(),
                MinFilter = descriptor.MinFilter.Convert(),
                MipmapFilter = descriptor.MipmapFilter.MipmapConvert(),
                LodMaxClamp = descriptor.LodMaxClamp,
                LodMinClamp = descriptor.LodMinClamp,
                MaxAnisotropy = descriptor.MaxAnisotropy,
            };

            var sampler = Wgpu.DeviceCreateSampler(Device, &wgpuDescriptor);
            return new WgpuSampler(Wgpu, sampler);
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

            var handle = Wgpu.DeviceCreateBuffer(Device, &descriptor);

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

            var handle = Wgpu.DeviceCreateBuffer(Device, &wgpuDescriptor);

            return new WgpuBuffer<T>(handle, size, Wgpu);
        }

        public override void Dispose()
        {
            if (_surface != null)
                Wgpu.SurfaceRelease(_surface);

            if (Device != null)
                Wgpu.DeviceRelease(Device);

            if (_adapter != null)
                Wgpu.AdapterRelease(_adapter);

            if (_instance != null)
                Wgpu.InstanceRelease(_instance);
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

        private static ulong AlignUp(ulong value, int alignment)
        {
            return (value + (ulong)alignment - 1) & ~((ulong)alignment - 1);
        }
    }
}
