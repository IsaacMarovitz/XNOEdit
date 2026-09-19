using Plume;

namespace Solaris
{
    /// <summary>
    /// Stages texture and buffer uploads from any thread and submits them from one.
    /// </summary>
    public sealed unsafe class SlUploader : IDisposable
    {
        private const ulong StagingBlockSize = 32 * 1024 * 1024;

        /// <summary>
        /// D3D12 requires placed footprint rows to be 256-byte aligned.
        /// </summary>
        private const uint RowAlignment = 256;

        private readonly struct PendingTextureCopy(
            nint texture, ulong bufferOffset, uint mipLevel, uint arrayIndex,
            uint width, uint height, uint rowWidthInTexels, RenderFormat format,
            uint destinationX, uint destinationY)
        {
            public readonly nint Texture = texture;
            public readonly ulong BufferOffset = bufferOffset;
            public readonly uint MipLevel = mipLevel;
            public readonly uint ArrayIndex = arrayIndex;
            public readonly uint Width = width;
            public readonly uint Height = height;
            public readonly uint RowWidthInTexels = rowWidthInTexels;
            public readonly RenderFormat Format = format;
            public readonly uint DestinationX = destinationX;
            public readonly uint DestinationY = destinationY;
        }

        private readonly SlDevice _device;
        private readonly Lock _lock = new();

        private readonly List<SlBuffer> _blocks = [];
        private readonly List<PendingTextureCopy> _pending = [];
        private readonly HashSet<nint> _pendingTextures = [];

        private RenderCommandList* _commandList;
        private RenderCommandFence* _fence;

        private int _currentBlock;
        private ulong _cursor;
        private bool _disposed;

        public SlUploader(SlDevice device)
        {
            _device = device;

            _commandList = device.Queue->CreateCommandList();
            _fence = device.Handle->CreateCommandFence();

            _blocks.Add(device.CreateBuffer(StagingBlockSize, SlBufferUsage.Upload, "UploadStaging"));
        }

        public bool HasPendingWork
        {
            get
            {
                lock (_lock)
                {
                    return _pending.Count > 0;
                }
            }
        }

        /// <summary>
        /// Copies pixels into staging and queues the GPU copy. Safe to call from any thread.
        /// </summary>
        public void StageTexture(
            SlTexture texture,
            ReadOnlySpan<byte> source,
            uint width,
            uint height,
            uint bytesPerRow,
            uint sourceRowPitch = 0,
            uint mipLevel = 0,
            uint arrayIndex = 0,
            uint destinationX = 0,
            uint destinationY = 0)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(texture);

            if (sourceRowPitch == 0)
            {
                sourceRowPitch = bytesPerRow;
            }

            var bytesPerTexel = bytesPerRow / Math.Max(width, 1u);
            var alignedBytesPerRow = (bytesPerRow + RowAlignment - 1) & ~(RowAlignment - 1);
            var totalSize = (ulong)alignedBytesPerRow * height;

            lock (_lock)
            {
                var (block, offset) = Reserve(totalSize);
                var destination = block.AsSpan();

                for (var row = 0u; row < height; row++)
                {
                    var sourceRow = source.Slice((int)(row * sourceRowPitch), (int)bytesPerRow);
                    var destinationStart = (int)(offset + row * alignedBytesPerRow);

                    sourceRow.CopyTo(destination.Slice(destinationStart, (int)bytesPerRow));
                }

                var rowWidthInTexels = bytesPerTexel > 0 ? alignedBytesPerRow / bytesPerTexel : width;

                _pending.Add(new PendingTextureCopy(
                    (nint)texture.Handle, offset, mipLevel, arrayIndex,
                    width, height, rowWidthInTexels, texture.Format,
                    destinationX, destinationY));

                _pendingTextures.Add((nint)texture.Handle);
            }
        }

        /// <summary>
        /// Records every staged copy, submits, and waits for completion. Call from the
        /// thread that owns the queue, before the frame graph begins a frame.
        /// </summary>
        public void Flush()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_lock)
            {
                if (_pending.Count == 0)
                    return;

                _commandList->Begin();

                var textures = _pendingTextures.ToArray();
                TransitionTextures(textures, RenderTextureLayout.CopyDest);

                foreach (var copy in _pending)
                {
                    var texture = (RenderTexture*)copy.Texture;

                    var destination = RenderTextureCopyLocation.WithSubresource(texture, copy.MipLevel, copy.ArrayIndex);

                    var source = RenderTextureCopyLocation.WithPlacedFootprint(
                        _blocks[0].Handle,
                        copy.Format,
                        copy.Width,
                        copy.Height,
                        1,
                        copy.RowWidthInTexels,
                        copy.BufferOffset);

                    _commandList->CopyTextureRegion(&destination, &source, copy.DestinationX, copy.DestinationY);
                }

                TransitionTextures(textures, RenderTextureLayout.ShaderRead);

                _commandList->End();

                var list = _commandList;
                _device.Queue->ExecuteCommandLists(&list, 1, null, 0, null, 0, _fence);
                _device.Queue->WaitForCommandFence(_fence);

                _pending.Clear();
                _pendingTextures.Clear();
                _currentBlock = 0;
                _cursor = 0;
            }
        }

        private void TransitionTextures(nint[] textures, RenderTextureLayout layout)
        {
            var barriers = stackalloc RenderTextureBarrier[textures.Length];

            for (var i = 0; i < textures.Length; i++)
            {
                barriers[i] = new RenderTextureBarrier((RenderTexture*)textures[i], layout);
            }

            _commandList->Barriers(RenderBarrierStages.Copy,
                new ReadOnlySpan<RenderTextureBarrier>(barriers, textures.Length));
        }

        private (SlBuffer Block, ulong Offset) Reserve(ulong size)
        {
            if (size > StagingBlockSize)
                throw new ArgumentOutOfRangeException(nameof(size),
                    $"A single mip level of {size} bytes exceeds the {StagingBlockSize} byte staging block.");

            var aligned = (_cursor + RowAlignment - 1) & ~(RowAlignment - 1);

            if (aligned + size > StagingBlockSize)
            {
                throw new InvalidOperationException(
                    $"The staging arena is full with {_pending.Count} pending copies. " +
                    "Flush more often, or raise StagingBlockSize.");
            }

            _cursor = aligned + size;
            return (_blocks[_currentBlock], aligned);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_fence != null)
            {
                _fence->Dispose();
                _fence = null;
            }

            if (_commandList != null)
            {
                _commandList->Dispose();
                _commandList = null;
            }

            foreach (var block in _blocks)
            {
                block.Dispose();
            }

            _blocks.Clear();
        }
    }
}
