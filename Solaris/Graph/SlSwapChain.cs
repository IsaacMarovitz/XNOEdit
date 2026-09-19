using Plume;

namespace Solaris.Graph
{
    internal sealed unsafe class SlSwapChain : IDisposable
    {
        private readonly SlDevice _device;
        private RenderSwapChain* _swapChain;

        private readonly RenderCommandSemaphore*[] _acquireSemaphores = new RenderCommandSemaphore*[SlDevice.FramesInFlight];
        private readonly RenderCommandSemaphore*[] _presentSemaphores = new RenderCommandSemaphore*[SlDevice.FramesInFlight];
        private readonly RenderCommandFence*[] _fences = new RenderCommandFence*[SlDevice.FramesInFlight];
        private readonly RenderCommandList*[] _commandLists = new RenderCommandList*[SlDevice.FramesInFlight];

        private readonly Dictionary<uint, nint> _framebuffers = [];
        private readonly Dictionary<uint, SlTexture> _textures = [];

        private bool _disposed;

        public SlSwapChain(SlDevice device, RenderWindow window, RenderFormat format, uint textureCount = 2, uint maxFrameLatency = 0)
        {
            _device = device;
            Format = format;

            var desc = new RenderSwapChainDesc
            {
                RenderWindow = window,
                Format = format,
                TextureCount = textureCount,
                EnablePresentWait = device.Capabilities.PresentWait,
                MaxFrameLatency = maxFrameLatency,
            };

            _swapChain = device.Queue->CreateSwapChain(&desc);

            if (_swapChain == null)
                throw new InvalidOperationException("Failed to create the swap chain.");

            for (var i = 0; i < SlDevice.FramesInFlight; i++)
            {
                _acquireSemaphores[i] = device.Handle->CreateCommandSemaphore();
                _presentSemaphores[i] = device.Handle->CreateCommandSemaphore();
                _fences[i] = device.Handle->CreateCommandFence();
                _commandLists[i] = device.Queue->CreateCommandList();
            }

            // The constructor's swap chain has no textures until sized once.
            Resize();
        }

        public RenderFormat Format { get; }

        public uint Width => _swapChain->GetWidth();

        public uint Height => _swapChain->GetHeight();

        public bool NeedsResize => _swapChain->NeedsResize();

        public bool IsEmpty => _swapChain->IsEmpty();

        public void SetVsyncEnabled(bool enabled) => _swapChain->SetVsyncEnabled(enabled);

        public bool Resize()
        {
            InvalidateFramebuffers();

            if (!_swapChain->Resize())
                return false;

            return !_swapChain->IsEmpty();
        }

        public bool AcquireTexture(int frameSlot, out uint textureIndex)
        {
            uint index = 0;
            var acquired = _swapChain->AcquireTexture(_acquireSemaphores[frameSlot], &index);
            textureIndex = index;
            return acquired;
        }

        /// <summary>
        /// The back buffer as an <see cref="SlTexture"/>. Wrappers are cached per index
        /// and do not own the underlying texture — the swap chain does.
        /// </summary>
        public SlTexture GetTexture(uint textureIndex)
        {
            if (_textures.TryGetValue(textureIndex, out var cached))
                return cached;

            var handle = _swapChain->GetTexture(textureIndex);

            var descriptor = new SlTextureDescriptor
            {
                Width = Width,
                Height = Height,
                Format = Format,
                Usage = SlTextureUsage.ColorTarget,
            };

            var texture = new SlTexture(handle, descriptor, ownsTexture: false);
            _textures[textureIndex] = texture;
            return texture;
        }

        public RenderCommandList* CommandList(int frameSlot) => _commandLists[frameSlot];

        public RenderCommandFence* Fence(int frameSlot) => _fences[frameSlot];

        public RenderCommandSemaphore* AcquireSemaphore(int frameSlot) => _acquireSemaphores[frameSlot];

        public RenderCommandSemaphore* PresentSemaphore(int frameSlot) => _presentSemaphores[frameSlot];

        public bool Present(uint textureIndex, int frameSlot)
        {
            var semaphore = _presentSemaphores[frameSlot];
            return _swapChain->Present(textureIndex, &semaphore, 1);
        }

        private void InvalidateFramebuffers()
        {
            foreach (var framebuffer in _framebuffers.Values)
            {
                var handle = (RenderFramebuffer*)framebuffer;
                _device.Retirement.Retire(() => handle->Dispose());
            }

            _framebuffers.Clear();

            foreach (var texture in _textures.Values)
            {
                texture.Dispose();
            }

            _textures.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            InvalidateFramebuffers();

            for (var i = 0; i < SlDevice.FramesInFlight; i++)
            {
                _commandLists[i]->Dispose();
                _fences[i]->Dispose();
                _presentSemaphores[i]->Dispose();
                _acquireSemaphores[i]->Dispose();
            }

            if (_swapChain != null)
            {
                _swapChain->Dispose();
                _swapChain = null;
            }
        }
    }
}
