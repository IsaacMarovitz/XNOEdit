using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using Plume;
using SDL3;
using Solaris;

namespace XNOEdit.Panels
{
    public class ViewportPanel : IDisposable
    {
        public const string Name = "Viewport";
        public Vector2 ViewportSize { get; private set; } = new(800, 600);
        public bool IsHovered { get; private set; }

        private readonly SlDevice _device;

        private SlTexture? _colorTexture;
        private SlTexture? _depthTexture;
        private SlTexture? _resolveTexture;
        private SlTextureIndex _resolveIndex;

        private uint _sampleCount = 4;
        private float _renderScale = 1.0f;

        public SlTexture ColorTarget => _colorTexture!;
        public SlTexture DepthTarget => _depthTexture!;
        public SlTexture ResolveTarget => _resolveTexture!;
        public bool IsMultisampled => _sampleCount > 1;

        private const RenderFormat ColorTextureFormat = RenderFormat.B8G8R8A8Unorm;
        private const RenderFormat DepthTextureFormat = RenderFormat.D32Float;

        public ViewportPanel(SlDevice device, nint window)
        {
            _device = device;
            _renderScale = SDL.GetWindowPixelDensity(window);

            CreateRenderTargets((uint)ViewportSize.X, (uint)ViewportSize.Y);
        }

        private void CreateRenderTargets(uint width, uint height)
        {
            width = Math.Max((uint)(width * _renderScale), 1);
            height = Math.Max((uint)(height * _renderScale), 1);

            var supported = _device.GetSampleCountsSupported(ColorTextureFormat);
            var samples = _sampleCount;

            while (samples > 1 && (supported & samples) == 0)
            {
                samples /= 2;
            }

            _colorTexture = _device.CreateTexture(
                SlTextureDescriptor.ColorTarget(width, height, ColorTextureFormat, samples), "ViewportColor");

            _depthTexture = _device.CreateTexture(
                SlTextureDescriptor.DepthTarget(width, height, DepthTextureFormat, samples), "ViewportDepth");

            _resolveTexture = samples > 1
                ? _device.CreateTexture(
                    SlTextureDescriptor.ColorTarget(width, height, ColorTextureFormat), "ViewportResolve")
                : _colorTexture;

            _resolveIndex = _device.Tables.Register(_resolveTexture);
        }

        private void DestroyRenderTargets()
        {
            if (_resolveTexture != null)
            {
                _device.Tables.Release(_resolveIndex);

                if (!ReferenceEquals(_resolveTexture, _colorTexture))
                    _device.Retire(_resolveTexture);
            }

            if (_colorTexture != null)
            {
                _device.Retire(_colorTexture);
            }

            if (_depthTexture != null)
            {
                _device.Retire(_depthTexture);
            }

            _colorTexture = null;
            _depthTexture = null;
            _resolveTexture = null;
        }

        public void Resize(uint width, uint height)
        {
            if (width == (uint)ViewportSize.X && height == (uint)ViewportSize.Y)
                return;

            DestroyRenderTargets();
            CreateRenderTargets(width, height);

            ViewportSize = new Vector2(width, height);
        }

        public void Render(Matrix4x4 view, bool renderGuizmos)
        {
            ImGuiInterop.SetNextWindowClass(new ImGuiWindowClass
            {
                DockNodeFlagsOverrideSet = (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.NoTabBar
            });

            var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                              ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));

            if (ImGui.Begin(Name, windowFlags))
            {
                var contentSize = ImGui.GetContentRegionAvail();
                var windowPos = ImGui.GetWindowPos();

                if (renderGuizmos)
                {
                    ImGuizmo.SetDrawlist(ImGui.GetWindowDrawList());
                    ImGuizmo.SetRect(windowPos.X, windowPos.Y, contentSize.X, contentSize.Y);
                    ImGuizmo.SetImGuiContext(ImGui.GetCurrentContext());
                }

                IsHovered = ImGui.IsWindowHovered();

                // Get available content region size and store for next frame's resize
                _pendingWidth = (uint)Math.Max(contentSize.X, 1);
                _pendingHeight = (uint)Math.Max(contentSize.Y, 1);

                ImGuiInterop.Image(_resolveIndex.Packed, ViewportSize);

                if (renderGuizmos)
                {
                    const int size = 100;
                    var leftMost = windowPos.X + contentSize.X - size;
                    var position = new Vector2(leftMost, windowPos.Y);

                    ImGuizmo.ViewManipulate(ref view, 0, position, new Vector2(size, size), 0);
                }
            }

            ImGui.End();
            ImGui.PopStyleVar();
        }

        public void PrepareFrame()
        {
            if (_pendingWidth > 0 && _pendingHeight > 0 &&
                (_pendingWidth != (uint)ViewportSize.X || _pendingHeight != (uint)ViewportSize.Y))
            {
                Resize(_pendingWidth, _pendingHeight);
            }
        }

        private uint _pendingWidth;
        private uint _pendingHeight;

        public float GetAspectRatio()
        {
            return ViewportSize.X / Math.Max(ViewportSize.Y, 1);
        }

        public void Dispose()
        {
            DestroyRenderTargets();
        }
    }
}
