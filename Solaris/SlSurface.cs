namespace Solaris
{
    public abstract class SlSurface
    {
        /// <summary>
        /// Acquire the next presentable texture from the swapchain.
        /// Returns null if the surface is out of date or otherwise unavailable.
        /// </summary>
        public abstract SlTexture? GetCurrentTexture();

        /// <summary>
        /// Present the current frame to the display.
        /// </summary>
        public abstract void Present();

        /// <summary>
        /// Reconfigure the surface (e.g. on resize).
        /// </summary>
        public abstract void Configure(SlSurfaceDescriptor descriptor);

        public abstract void Dispose();
    }
}
