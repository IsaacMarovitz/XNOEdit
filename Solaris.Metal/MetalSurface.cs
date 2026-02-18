using System.Runtime.Versioning;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;
using SDL3;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalSurface : SlSurface
    {
        private readonly MetalDevice _device;
        private readonly IntPtr _metalView;
        private CAMetalLayer _layer;
        private CAMetalDrawable _currentDrawable;
        private MTLTexture _previousDrawableTexture;

        internal CAMetalDrawable CurrentDrawable => _currentDrawable;

        internal MetalSurface(MetalDevice device, IntPtr window)
        {
            _device = device;

            _metalView = SDL.MetalCreateView(window);
            var layerPtr = SDL.MetalGetLayer(_metalView);
            _layer = new CAMetalLayer(layerPtr);

            _layer.Device = device.Device;
            _layer.PixelFormat = SlDevice.SurfaceFormat.Convert();
            _layer.FramebufferOnly = true;

            SDL.GetWindowSizeInPixels(window, out var w, out var h);
            // _layer.DrawableSize = new CGSize { Width = w, Height = h };
        }

        public override SlTexture? GetCurrentTexture()
        {
            // Dispose the previous drawable if it exists
            if (_currentDrawable.NativePtr != IntPtr.Zero)
            {
                _currentDrawable.Dispose();
                _currentDrawable = default;
            }

            // Remove previous drawable's texture from residency
            if (_previousDrawableTexture.NativePtr != IntPtr.Zero)
            {
                _device.RemoveResident(_previousDrawableTexture);
            }

            _currentDrawable = _layer.NextDrawable;
            if (_currentDrawable.NativePtr == IntPtr.Zero)
                return null;

            _previousDrawableTexture = _currentDrawable.Texture;
            _device.MakeResident(_previousDrawableTexture);

            return new MetalTexture(_device, _currentDrawable.Texture, owned: false);
        }

        public override void Present()
        {
            if (_currentDrawable.NativePtr != IntPtr.Zero)
            {
                _currentDrawable.Present();
                _currentDrawable.Dispose();
                _currentDrawable = default;
            }
        }

        public override void Configure(SlSurfaceDescriptor descriptor)
        {
            _layer.PixelFormat = descriptor.Format.Convert();
            // _layer.DrawableSize = new CGSize { Width = descriptor.Width, Height = descriptor.Height };
        }

        public override void Dispose()
        {
            if (_metalView != IntPtr.Zero)
                SDL.MetalDestroyView(_metalView);
        }
    }
}
