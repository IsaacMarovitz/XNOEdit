using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using Solaris;
using XNOEdit.Renderer;

namespace XNOEdit.Panels
{
    public unsafe class ViewportPanel : IDisposable
    {
        public const string Name = "Viewport";
        public Vector2 ViewportSize { get; private set; } = new(800, 600);
        public bool IsHovered { get; private set; }

        private readonly SlDevice _device;
        private readonly ImGuiController _imguiController;

        private SlTexture? _colorTexture;
        private SlTextureView? _colorTextureView;
        private SlTexture? _depthTexture;
        private SlTextureView? _depthTextureView;

        private const SlTextureFormat ColorTextureFormat = SlTextureFormat.Bgra8Unorm;
        private const SlTextureFormat DepthTextureFormat = SlTextureFormat.Depth32float;

        public ViewportPanel(SlDevice device, ImGuiController imguiController)
        {
            _device = device;
            _imguiController = imguiController;

            CreateRenderTargets((uint)ViewportSize.X, (uint)ViewportSize.Y);
        }

        private void CreateRenderTargets(uint width, uint height)
        {
            // Ensure minimum size
            width = Math.Max(width, 1);
            height = Math.Max(height, 1);

            // Create color texture
            var colorTextureDesc = new SlTextureDescriptor
            {
                Size = new SlExtent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = SlTextureDimension.Dimension2D,
                Format = ColorTextureFormat,
                Usage = SlTextureUsage.RenderAttachment | SlTextureUsage.TextureBinding
            };

            _colorTexture = _device.CreateTexture(colorTextureDesc);

            var colorViewDesc = new SlTextureViewDescriptor
            {
                Format = ColorTextureFormat,
                Dimension = SlTextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1
            };

            _colorTextureView = _colorTexture.CreateTextureView(colorViewDesc);

            var depthTextureDesc = new SlTextureDescriptor
            {
                Size = new SlExtent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = SlTextureDimension.Dimension2D,
                Format = DepthTextureFormat,
                Usage = SlTextureUsage.RenderAttachment
            };

            _depthTexture = _device.CreateTexture(depthTextureDesc);

            var depthViewDesc = new SlTextureViewDescriptor
            {
                Format = DepthTextureFormat,
                Dimension = SlTextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1
            };

            _depthTextureView = _depthTexture.CreateTextureView(depthViewDesc);
            _imguiController.BindImGuiTextureView(_colorTextureView);
        }

        private void DestroyRenderTargets()
        {
            if (_colorTextureView != null)
            {
                _imguiController.UnbindImGuiTextureView((IntPtr)_colorTextureView.GetHandle());
                _colorTextureView.Dispose();
            }

            _colorTexture?.Dispose();
            _depthTextureView?.Dispose();
            _depthTexture?.Dispose();

            _colorTexture = null;
            _colorTextureView = null;
            _depthTexture = null;
            _depthTextureView = null;
        }

        public void Resize(uint width, uint height)
        {
            if (width == (uint)ViewportSize.X && height == (uint)ViewportSize.Y)
                return;

            DestroyRenderTargets();
            CreateRenderTargets(width, height);

            ViewportSize = new Vector2(width, height);
        }

        public SlRenderPass BeginRenderPass(SlCommandEncoder encoder)
        {
            var colorAttachment = new SlColorAttachment
            {
                View = _colorTextureView,
                LoadOp = SlLoadOp.Clear,
                StoreOp = SlStoreOp.Store,
                ClearValue = new SlColor { R = 0.1, G = 0.1, B = 0.1, A = 1.0 }
            };

            var depthAttachment = new SlDepthStencilAttachment
            {
                View = _depthTextureView,
                DepthLoadOp = SlLoadOp.Clear,
                DepthStoreOp = SlStoreOp.Store,
                DepthClearValue = 0.0f // Reverse-Z
            };

            var renderPassDesc = new SlRenderPassDescriptor
            {
                ColorAttachments = [colorAttachment],
                DepthStencilAttachment = depthAttachment
            };

            return encoder.BeginRenderPass(renderPassDesc);
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

                ImGui.Image(new ImTextureRef(null, _colorTextureView.GetHandle()), ViewportSize);

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
