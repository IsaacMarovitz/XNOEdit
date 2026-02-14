using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    public static class WgpuBackend
    {
        /// <summary>
        /// Registers the WebGPU backend with the device factory.
        /// Call this once at application startup before creating any devices.
        /// </summary>
        public static void Register()
        {
            SlDeviceFactory.Register(SlBackend.Wgpu, window => new WgpuDevice(WebGPU.GetApi(), window));
        }
    }
}
