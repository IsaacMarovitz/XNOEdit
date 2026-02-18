using System.Runtime.Versioning;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    public static class MetalBackend
    {
        /// <summary>
        /// Registers the Metal 4 backend with the device factory.
        /// Call this once at application startup before creating any devices.
        /// </summary>
        public static void Register()
        {
            SlDeviceFactory.Register(SlBackend.Metal4, window => new MetalDevice(window));
        }
    }
}
