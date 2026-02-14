namespace Solaris
{
    public enum SlBackend
    {
        Wgpu,
    }

    /// <summary>
    /// Factory for creating backend-specific devices without referencing backend types directly.
    /// Each backend project registers itself at startup.
    /// </summary>
    public static class SlDeviceFactory
    {
        public delegate SlDevice DeviceCreateFunc(IntPtr window);

        private static readonly Dictionary<SlBackend, DeviceCreateFunc> _factories = new();

        /// <summary>
        /// Called by backend projects to register their device factory.
        /// e.g. SlDeviceFactory.Register(SlBackend.WebGpu, window => new WgpuDevice(WebGPU.GetApi(), window));
        /// </summary>
        public static void Register(SlBackend backend, DeviceCreateFunc factory)
        {
            _factories[backend] = factory;
        }

        /// <summary>
        /// Create a device for the given backend.
        /// </summary>
        public static SlDevice Create(SlBackend backend, IntPtr window)
        {
            if (!_factories.TryGetValue(backend, out var factory))
                throw new InvalidOperationException(
                    $"No factory registered for backend '{backend}'. " +
                    $"Ensure the backend project is referenced and has called SlDeviceFactory.Register().");

            return factory(window);
        }

        /// <summary>
        /// Returns true if a factory has been registered for the given backend.
        /// </summary>
        public static bool IsAvailable(SlBackend backend) => _factories.ContainsKey(backend);
    }
}
