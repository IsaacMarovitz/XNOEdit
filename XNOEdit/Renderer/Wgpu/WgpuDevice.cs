using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Silk.NET.Windowing;

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

        public WgpuDevice(WebGPU wgpu, IWindow window)
        {
            _wgpu = wgpu;

            var extras = new InstanceExtras
            {
                Chain = new ChainedStruct { SType = (SType)NativeSType.STypeInstanceExtras },
                Backends = InstanceBackend.Vulkan
            };

            var instanceDesc = new InstanceDescriptor
            {
                // NextInChain = (ChainedStruct*)&extras
            };

            _instance = _wgpu.CreateInstance(&instanceDesc);
            _surface = window.CreateWebGPUSurface(_wgpu, _instance);

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
            Console.WriteLine($"Backend type: {adapterProps.BackendType}");

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

            // Configure surface
            var surfaceConfig = new SurfaceConfiguration
            {
                Device = Device,
                Format = _surfaceFormat,
                Usage = TextureUsage.RenderAttachment,
                Width = (uint)window.FramebufferSize.X,
                Height = (uint)window.FramebufferSize.Y,
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
    }
}
