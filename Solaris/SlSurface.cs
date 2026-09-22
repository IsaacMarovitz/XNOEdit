using Plume;
using SDL3;

namespace Solaris
{
    public readonly struct SlSurface
    {
        private readonly nint _window;
        private readonly nint _view;

        private SlSurface(nint window, nint view)
        {
            _window = window;
            _view = view;
        }

        public static SlSurface FromSdlWindow(nint window)
        {
            var view = SDL.MetalCreateView(window);
            var cocoaWindow = SDL.GetPointerProperty(
                SDL.GetWindowProperties(window), SDL.Props.WindowCocoaWindowPointer, IntPtr.Zero);

            return new SlSurface(cocoaWindow, SDL.MetalGetLayer(view));
        }

        internal unsafe RenderWindow ToPlume() => new()
        {
            Window = (void*)_window,
            View = (void*)_view,
        };
    }
}
