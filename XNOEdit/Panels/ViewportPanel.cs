using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using Plume;
using Solaris;

namespace XNOEdit.Panels
{
    public unsafe class ViewportPanel : IDisposable
    {
        public const string Name = "Viewport";
        public Vector2 ViewportSize { get; private set; } = new(800, 600);
        public bool IsHovered { get; private set; }

        private readonly SlDevice _device;

        private SlTexture? _colorTexture;
        private SlTexture? _depthTexture;
        private SlTextureIndex _colorIndex;

        public SlTexture ColorTarget => _colorTexture!;
        public SlTexture DepthTarget => _depthTexture!;

        private const RenderFormat ColorTextureFormat = RenderFormat.B8G8R8A8Unorm;
        private const RenderFormat DepthTextureFormat = RenderFormat.D32Float;

        public ViewportPanel(SlDevice device)
        {
            _device = device;

            CreateRenderTargets((uint)ViewportSize.X, (uint)ViewportSize.Y);
        }

        private void CreateRenderTargets(uint width, uint height)
        {
            width = Math.Max(width, 1);
            height = Math.Max(height, 1);

            _colorTexture = _device.CreateTexture(
                SlTextureDescriptor.ColorTarget(width, height, ColorTextureFormat), "ViewportColor");

            _depthTexture = _device.CreateTexture(
                SlTextureDescriptor.DepthTarget(width, height, DepthTextureFormat), "ViewportDepth");

            _colorIndex = _device.Tables.Register(_colorTexture);
        }

        private void DestroyRenderTargets()
        {
            if (_colorTexture != null)
            {
                _device.Tables.Release(_colorIndex);
                _device.Retire(_colorTexture);
            }

            if (_depthTexture != null)
            {
                _device.Retire(_depthTexture);
            }

            _colorTexture = null;
            _depthTexture = null;
        }

        public void Resize(uint width, uint height)
        {
            if (width == (uint)ViewportSize.X && height == (uint)ViewportSize.Y)
                return;

            DestroyRenderTargets();
            CreateRenderTargets(width, height);

            ViewportSize = new Vector2(width, height);
        }

        public void Render(Matrix4x4 view, Matrix4x4 projection, bool renderGuizmos)
        {
            var windowClass = new ImGuiWindowClass
            {
                DockNodeFlagsOverrideSet = (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.NoTabBar
            };

            var ptr = new ImGuiWindowClassPtr(&windowClass);

            ImGui.SetNextWindowClass(ptr);
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

                ImGui.Image(new ImTextureRef(null, _colorIndex.Packed), ViewportSize);

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
