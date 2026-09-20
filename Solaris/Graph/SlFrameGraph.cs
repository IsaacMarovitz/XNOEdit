using Plume;

namespace Solaris.Graph
{
    /// <summary>
    /// Owns the swap chain, the per-frame upload rings, and the frame timeline. One
    /// instance per window; <see cref="BeginFrame"/> returns the frame to declare passes on.
    /// </summary>
    public sealed unsafe class SlFrameGraph : IDisposable
    {
        private const ulong RingBlockSize = 8 * 1024 * 1024 * 4;

        private readonly SlDevice _device;
        private readonly SlSwapChain _swapChain;
        private readonly SlUploadRing[] _rings = new SlUploadRing[SlDevice.FramesInFlight];
        private readonly ISlPassScheduler _scheduler = new SlLinearScheduler();

        private readonly Dictionary<int, SlResourceEntry> _resources = [];
        private int _nextResourceId = 1;
        private int _frameSlot;
        private readonly bool[] _slotInFlight = new bool[SlDevice.FramesInFlight];
        private bool _disposed;

        public SlFrameGraph(SlDevice device, RenderWindow window, RenderFormat format, uint maxFrameLatency = 0)
        {
            _device = device;
            _swapChain = new SlSwapChain(device, window, format, 2, maxFrameLatency);

            for (var i = 0; i < SlDevice.FramesInFlight; i++)
            {
                _rings[i] = new SlUploadRing(device, RingBlockSize);
            }
        }

        public SlDevice Device => _device;

        public uint Width => _swapChain.Width;

        public uint Height => _swapChain.Height;

        public RenderFormat SwapChainFormat => _swapChain.Format;

        public void SetVsyncEnabled(bool enabled) => _swapChain.SetVsyncEnabled(enabled);

        /// <summary>
        /// Recreates the swap chain's textures. Framebuffers and back buffer wrappers are
        /// invalidated, and imported handles for them become stale, so call this outside
        /// a frame.
        /// </summary>
        public bool Resize() => _swapChain.Resize();

        /// <summary>
        /// Begins a frame. Returns null when the swap chain has no presentable surface —
        /// a minimised or zero-sized window — in which case skip rendering entirely.
        /// </summary>
        public SlFrame? BeginFrame()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_swapChain.NeedsResize && !_swapChain.Resize())
                return null;

            if (_swapChain.IsEmpty)
                return null;

            if (_slotInFlight[_frameSlot])
            {
                _device.Queue->WaitForCommandFence(_swapChain.Fence(_frameSlot));
                _slotInFlight[_frameSlot] = false;
            }

            _device.Retirement.BeginFrame();
            _rings[_frameSlot].Reset();

            if (!_swapChain.AcquireTexture(_frameSlot, out var textureIndex))
            {
                _swapChain.Resize();
                return null;
            }

            return new SlFrame(this, _frameSlot, textureIndex);
        }

        internal SlSwapChain SwapChain => _swapChain;

        internal SlUploadRing Ring(int frameSlot) => _rings[frameSlot];

        internal ISlPassScheduler Scheduler => _scheduler;

        internal Dictionary<int, SlResourceEntry> Resources => _resources;

        internal int AllocateResourceId() => _nextResourceId++;

        internal void EndFrame(uint textureIndex)
        {
            _slotInFlight[_frameSlot] = true;
            _swapChain.Present(textureIndex, _frameSlot);

            _frameSlot = (_frameSlot + 1) % SlDevice.FramesInFlight;
        }

        public void WaitForIdle()
        {
            for (var i = 0; i < SlDevice.FramesInFlight; i++)
            {
                if (!_slotInFlight[i])
                    continue;

                _device.Queue->WaitForCommandFence(_swapChain.Fence(i));
                _slotInFlight[i] = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            WaitForIdle();

            foreach (var ring in _rings)
            {
                ring.Dispose();
            }

            _swapChain.Dispose();
        }
    }

    /// <summary>
    /// One frame's worth of pass declarations. Disposing submits: the graph schedules the
    /// passes, derives every barrier from the declared accesses, records the command list,
    /// and presents.
    /// </summary>
    public sealed unsafe class SlFrame : IDisposable
    {
        private readonly SlFrameGraph _graph;
        private readonly int _frameSlot;
        private readonly uint _textureIndex;
        private readonly List<SlPassNode> _passes = [];
        private readonly List<int> _transientIds = [];

        private bool _submitted;

        internal SlFrame(SlFrameGraph graph, int frameSlot, uint textureIndex)
        {
            _graph = graph;
            _frameSlot = frameSlot;
            _textureIndex = textureIndex;

            var backBuffer = graph.SwapChain.GetTexture(textureIndex);
            SwapChainTarget = RegisterResource("SwapChain", backBuffer, isTransient: false, isSwapChainTarget: true);
        }

        public SlTextureHandle SwapChainTarget { get; }

        public SlUploadRing Ring => _graph.Ring(_frameSlot);

        public uint Width => _graph.Width;

        public uint Height => _graph.Height;

        public SlTextureHandle ImportTexture(SlTexture texture)
        {
            ArgumentNullException.ThrowIfNull(texture);

            foreach (var entry in _graph.Resources.Values)
            {
                if (ReferenceEquals(entry.Texture, texture))
                    return new SlTextureHandle(entry.Id, entry.Version);
            }

            return RegisterResource("Imported", texture, isTransient: false, isSwapChainTarget: false);
        }

        public SlTextureHandle CreateTransientTarget(in SlTextureDescriptor descriptor, string name = "Transient")
        {
            var texture = _graph.Device.CreateTexture(descriptor, name);
            var handle = RegisterResource(name, texture, isTransient: true, isSwapChainTarget: false);
            _transientIds.Add(handle.Id);
            return handle;
        }

        public SlPassBuilder AddPass(string name)
        {
            var node = new SlPassNode { Index = _passes.Count, Name = name };
            _passes.Add(node);
            return new SlPassBuilder(this, node);
        }

        private SlTextureHandle RegisterResource(string name, SlTexture texture, bool isTransient, bool isSwapChainTarget)
        {
            var id = _graph.AllocateResourceId();

            var entry = new SlResourceEntry
            {
                Id = id,
                Name = name,
                Texture = texture,
                IsTransient = isTransient,
                IsSwapChainTarget = isSwapChainTarget,
            };

            _graph.Resources[id] = entry;
            return new SlTextureHandle(id, 0);
        }

        internal SlResourceEntry Resolve(SlTextureHandle handle)
        {
            if (!_graph.Resources.TryGetValue(handle.Id, out var entry))
                throw new ArgumentException($"Unknown resource {handle}.", nameof(handle));

            return entry;
        }

        /// <summary>Submits the frame. Prefer a <c>using</c> block over calling this directly.</summary>
        public void Dispose()
        {
            if (_submitted)
                return;

            _submitted = true;

            var commandList = _graph.SwapChain.CommandList(_frameSlot);
            var order = _graph.Scheduler.Schedule(_passes);

            commandList->Begin();

            var tables = _graph.Device.Tables;

            foreach (var passIndex in order)
            {
                RecordPass(commandList, _passes[passIndex], tables);
            }

            TransitionToPresent(commandList);

            commandList->End();

            Submit(commandList);

            foreach (var id in _transientIds)
            {
                if (!_graph.Resources.Remove(id, out var entry) || entry.Texture is not { } texture)
                    continue;

                _graph.Device.Retire(texture);
            }

            _graph.EndFrame(_textureIndex);
        }

        private void RecordPass(RenderCommandList* commandList, SlPassNode pass, SlBindlessTables tables)
        {
            EmitBarriers(commandList, pass);

            // The global tables are bound per pass, never per draw. All three texture set
            // slots share one physical set; the shader views it as three typed arrays.
            commandList->SetGraphicsPipelineLayout(_graph.Device.Layout.Handle);
            commandList->SetGraphicsDescriptorSet(tables.TextureSet, SlGlobalLayout.TextureSet2D);
            commandList->SetGraphicsDescriptorSet(tables.TextureSet, SlGlobalLayout.TextureSet2DArray);
            commandList->SetGraphicsDescriptorSet(tables.TextureSet, SlGlobalLayout.TextureSetCube);
            commandList->SetGraphicsDescriptorSet(tables.SamplerSet, SlGlobalLayout.SamplerSet);
            commandList->SetGraphicsDescriptorSet(Ring.DescriptorSet, SlGlobalLayout.ConstantSet);

            var zeroes = stackalloc byte[(int)SlGlobalLayout.PushConstantSize];
            new Span<byte>(zeroes, (int)SlGlobalLayout.PushConstantSize).Clear();
            commandList->SetGraphicsPushConstants(0, zeroes, 0, SlGlobalLayout.PushConstantSize);

            var framebuffer = AcquireFramebuffer(pass, out var width, out var height);

            if (framebuffer != null)
            {
                commandList->SetFramebuffer(framebuffer);
                commandList->SetViewports(new RenderViewport(0, 0, width, height));
                commandList->SetScissors(new RenderRect(0, 0, (int)width, (int)height));

                ApplyClears(commandList, pass);
            }

            if (pass.Body == null)
                return;

            var context = new SlPassContext(this, commandList, width, height, BuildSignature(pass));
            pass.Body(context);
            EmitResolve(commandList, pass);
        }

        private void EmitResolve(RenderCommandList* commandList, SlPassNode pass)
        {
            if (pass.ResolveSource is not { } sourceHandle || pass.ResolveTarget is not { } targetHandle)
                return;

            var source = Resolve(sourceHandle);
            var target = Resolve(targetHandle);

            if (source.Texture is not { } sourceTexture || target.Texture is not { } targetTexture)
                return;

            var barriers = stackalloc RenderTextureBarrier[2];

            barriers[0] = new RenderTextureBarrier(sourceTexture.Handle, RenderTextureLayout.ResolveSource);
            barriers[1] = new RenderTextureBarrier(targetTexture.Handle, RenderTextureLayout.ResolveDest);

            commandList->Barriers(RenderBarrierStages.Graphics, new ReadOnlySpan<RenderTextureBarrier>(barriers, 2));

            source.Layout = RenderTextureLayout.ResolveSource;
            target.Layout = RenderTextureLayout.ResolveDest;

            commandList->ResolveTexture(targetTexture.Handle, sourceTexture.Handle);
        }

        /// <summary>
        /// Derives the pass's attachment configuration from its declared accesses, so a
        /// draw can resolve the right pipeline without knowing which pass it landed in.
        /// </summary>
        private SlPassSignature BuildSignature(SlPassNode pass)
        {
            var colorFormat = RenderFormat.Unknown;
            var depthFormat = RenderFormat.Unknown;
            var sampleCount = 1u;
            var colorCount = 0u;

            foreach (var access in pass.Accesses)
            {
                var entry = Resolve(access.Handle);

                if (entry.Texture is not { } texture)
                    continue;

                switch (access.Access)
                {
                    case SlAccess.ColorTarget:
                        if (colorCount == 0)
                        {
                            colorFormat = texture.Format;
                            sampleCount = texture.Descriptor.SampleCount;
                        }

                        colorCount++;
                        break;

                    case SlAccess.DepthTarget:
                        depthFormat = texture.Format;
                        break;
                }
            }

            return new SlPassSignature
            {
                ColorFormat0 = colorFormat,
                DepthFormat = depthFormat,
                SampleCount = sampleCount,
                ColorCount = colorCount,
            };
        }

        /// <summary>
        /// Derives barriers from the pass's declared accesses. A resource whose tracked
        /// layout differs from what the access requires gets a transition.
        /// </summary>
        private void EmitBarriers(RenderCommandList* commandList, SlPassNode pass)
        {
            var barriers = stackalloc RenderTextureBarrier[pass.Accesses.Count];
            var count = 0;

            foreach (var access in pass.Accesses)
            {
                var entry = Resolve(access.Handle);
                var required = access.Access.ToLayout();

                if (entry.Layout == required || entry.Texture is not { } texture)
                    continue;

                barriers[count++] = new RenderTextureBarrier(texture.Handle, required);
                entry.Layout = required;

                if (access.Access.IsWrite())
                {
                    entry.Version++;
                }
            }

            if (count > 0)
            {
                commandList->Barriers(RenderBarrierStages.Graphics, new ReadOnlySpan<RenderTextureBarrier>(barriers, count));
            }
        }

        private void ApplyClears(RenderCommandList* commandList, SlPassNode pass)
        {
            var colorIndex = 0u;

            foreach (var access in pass.Accesses)
            {
                switch (access.Access)
                {
                    case SlAccess.ColorTarget:
                        if (access.Load == SlLoadOp.Clear)
                        {
                            commandList->ClearColor(colorIndex, access.Clear.ToRenderColor());
                        }

                        colorIndex++;
                        break;

                    case SlAccess.DepthTarget:
                        if (access.Load == SlLoadOp.Clear)
                        {
                            commandList->ClearDepth(true, access.Clear.Depth);
                        }

                        break;
                }
            }
        }

        private RenderFramebuffer* AcquireFramebuffer(SlPassNode pass, out uint width, out uint height)
        {
            width = _graph.Width;
            height = _graph.Height;

            SlTexture? depth = null;
            var colors = stackalloc RenderTexture*[8];
            var colorCount = 0u;

            foreach (var access in pass.Accesses)
            {
                var entry = Resolve(access.Handle);

                switch (access.Access)
                {
                    case SlAccess.ColorTarget when entry.Texture is { } texture:
                        colors[colorCount++] = texture.Handle;
                        width = texture.Width;
                        height = texture.Height;
                        break;

                    case SlAccess.DepthTarget:
                        depth = entry.Texture;
                        break;
                }
            }

            if (colorCount == 0 && depth == null)
                return null;

            var desc = new RenderFramebufferDesc
            {
                ColorAttachments = colors,
                ColorAttachmentsCount = colorCount,
                DepthAttachment = depth != null ? depth.Handle : null,
            };

            // Plume copies the attachment list during construction,
            // so the stack array is safe here.
            var framebuffer = _graph.Device.Handle->CreateFramebuffer(&desc);

            if (framebuffer != null)
            {
                var captured = framebuffer;
                _graph.Device.Retirement.Retire(() => captured->Dispose());
            }

            return framebuffer;
        }

        private void TransitionToPresent(RenderCommandList* commandList)
        {
            var entry = Resolve(SwapChainTarget);

            if (entry.Texture is not { } texture || entry.Layout == RenderTextureLayout.Present)
                return;

            var barrier = new RenderTextureBarrier(texture.Handle, RenderTextureLayout.Present);
            commandList->Barriers(RenderBarrierStages.Graphics, barrier);
            entry.Layout = RenderTextureLayout.Present;
        }

        private void Submit(RenderCommandList* commandList)
        {
            var swapChain = _graph.SwapChain;

            var list = commandList;
            var acquire = swapChain.AcquireSemaphore(_frameSlot);
            var present = swapChain.PresentSemaphore(_frameSlot);

            _graph.Device.Queue->ExecuteCommandLists(
                &list, 1,
                &acquire, 1,
                &present, 1,
                swapChain.Fence(_frameSlot));
        }
    }

    /// <summary>
    /// Declares what a pass touches. Accesses are recorded before any recording happens,
    /// which is what allows barriers to be computed up front rather than discovered.
    /// </summary>
    public sealed class SlPassBuilder
    {
        private readonly SlFrame _frame;
        private readonly SlPassNode _node;

        internal SlPassBuilder(SlFrame frame, SlPassNode node)
        {
            _frame = frame;
            _node = node;
        }

        public SlPassBuilder Color(SlTextureHandle target, SlLoadOp load = SlLoadOp.Clear, SlClearValue clear = default)
        {
            _node.Accesses.Add(new SlPassAccess(target, SlAccess.ColorTarget, load, clear));
            return this;
        }

        public SlPassBuilder Depth(SlTextureHandle target, SlLoadOp load = SlLoadOp.Clear, float clear = 0.0f)
        {
            _node.Accesses.Add(new SlPassAccess(target, SlAccess.DepthTarget, load, SlClearValue.DepthValue(clear)));
            return this;
        }

        public SlPassBuilder Reads(SlTextureHandle texture)
        {
            _node.Accesses.Add(new SlPassAccess(texture, SlAccess.ShaderRead, SlLoadOp.Load, default));
            return this;
        }

        public SlPassBuilder ResolveTo(SlTextureHandle source, SlTextureHandle target)
        {
            _node.ResolveSource = source;
            _node.ResolveTarget = target;
            return this;
        }

        public void Execute(Action<SlPassContext> body)
        {
            ArgumentNullException.ThrowIfNull(body);

            _node.Body = body;
        }
    }
}
