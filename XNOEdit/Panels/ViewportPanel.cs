using System.Numerics;
using Hexa.NET.ImGui;
using Hexa.NET.ImGuizmo;
using Silk.NET.WebGPU;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Wgpu;

namespace XNOEdit.Panels
{
    public unsafe class ViewportPanel : IDisposable
    {
        public Vector2 ViewportSize { get; private set; } = new(800, 600);
        public bool IsHovered { get; private set; }

        private readonly WebGPU _wgpu;
        private readonly WgpuDevice _device;
        private readonly ImGuiController _imguiController;

        private Texture* _colorTexture;
        private TextureView* _colorTextureView;
        private Texture* _depthTexture;
        private TextureView* _depthTextureView;

        private const TextureFormat ColorTextureFormat = TextureFormat.Bgra8Unorm;
        private const TextureFormat DepthTextureFormat = TextureFormat.Depth32float;

        public ViewportPanel(WebGPU wgpu, WgpuDevice device, ImGuiController imguiController)
        {
            _wgpu = wgpu;
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
            var colorTextureDesc = new TextureDescriptor
            {
                Size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = ColorTextureFormat,
                Usage = TextureUsage.RenderAttachment | TextureUsage.TextureBinding
            };

            _colorTexture = _wgpu.DeviceCreateTexture(_device, &colorTextureDesc);

            var colorViewDesc = new TextureViewDescriptor
            {
                Format = ColorTextureFormat,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            _colorTextureView = _wgpu.TextureCreateView(_colorTexture, &colorViewDesc);

            var depthTextureDesc = new TextureDescriptor
            {
                Size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = DepthTextureFormat,
                Usage = TextureUsage.RenderAttachment
            };

            _depthTexture = _wgpu.DeviceCreateTexture(_device, &depthTextureDesc);

            var depthViewDesc = new TextureViewDescriptor
            {
                Format = DepthTextureFormat,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All
            };

            _depthTextureView = _wgpu.TextureCreateView(_depthTexture, &depthViewDesc);
            _imguiController.BindImGuiTextureView(_colorTextureView);
        }

        private void DestroyRenderTargets()
        {
            if (_colorTextureView != null)
            {
                _imguiController.UnbindImGuiTextureView(_colorTextureView);
                _wgpu.TextureViewRelease(_colorTextureView);
            }

            if (_colorTexture != null)
            {
                _wgpu.TextureDestroy(_colorTexture);
                _wgpu.TextureRelease(_colorTexture);
            }

            if (_depthTextureView != null)
                _wgpu.TextureViewRelease(_depthTextureView);

            if (_depthTexture != null)
            {
                _wgpu.TextureDestroy(_depthTexture);
                _wgpu.TextureRelease(_depthTexture);
            }

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

        public RenderPassEncoder* BeginRenderPass(CommandEncoder* encoder)
        {
            var colorAttachment = new RenderPassColorAttachment
            {
                View = _colorTextureView,
                LoadOp = LoadOp.Clear,
                StoreOp = StoreOp.Store,
                ClearValue = new Color { R = 0.1, G = 0.1, B = 0.1, A = 1.0 }
            };

            var depthAttachment = new RenderPassDepthStencilAttachment
            {
                View = _depthTextureView,
                DepthLoadOp = LoadOp.Clear,
                DepthStoreOp = StoreOp.Store,
                DepthClearValue = 0.0f // Reverse-Z
            };

            var renderPassDesc = new RenderPassDescriptor
            {
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
                DepthStencilAttachment = &depthAttachment
            };

            return _wgpu.CommandEncoderBeginRenderPass(encoder, &renderPassDesc);
        }

        public void Render(Matrix4x4 view, Matrix4x4 projection, bool renderGuizmos)
        {
            var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoDecoration;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));

            if (ImGui.Begin("Viewport", windowFlags))
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

                ImGui.Image(new ImTextureRef(null, _colorTextureView), ViewportSize);

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
