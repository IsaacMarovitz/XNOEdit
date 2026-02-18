using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    public class MetalDevice : SlDevice
    {
        private const int MaxFramesInFlight = 3;

        internal MTLDevice Device { get; }
        internal MTL4Compiler Compiler { get; }

        private readonly MetalSurface _surface;
        private readonly MetalQueue _queue;

        private MTLResidencySet _residencySet;
        private bool _residencyDirty;
        private readonly Lock _residencyLock = new();

        internal MTL4CommandBuffer CommandBuffer { get; }
        private readonly MTL4CommandAllocator[] _allocatorPool;

        private MTLSharedEvent _frameEvent;
        private ulong _frameCounter;

        internal int CurrentFrameIndex => (int)(_frameCounter % MaxFramesInFlight);

        internal MetalDevice(IntPtr window)
        {
            Device = MTLDevice.CreateSystemDefaultDevice();
            if (Device.NativePtr == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create Metal device");

            var compilerError = new NSError(IntPtr.Zero);
            Compiler = Device.NewCompiler(new MTL4CompilerDescriptor(), ref compilerError);
            if (compilerError != IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to create MTL4 compiler: {compilerError.LocalizedDescription}");

            CommandBuffer = Device.NewCommandBuffer;

            _allocatorPool = new MTL4CommandAllocator[MaxFramesInFlight];
            for (var i = 0; i < MaxFramesInFlight; i++)
                _allocatorPool[i] = Device.NewCommandAllocator();

            _frameEvent = Device.NewSharedEvent();
            _frameCounter = 0;

            var residencyDesc = new MTLResidencySetDescriptor();
            residencyDesc.InitialCapacity = 256;
            var residencyError = new NSError(IntPtr.Zero);
            _residencySet = Device.NewResidencySet(residencyDesc, ref residencyError);
            if (residencyError != IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to create residency set: {residencyError.LocalizedDescription}");

            residencyDesc.Dispose();

            var commandQueue = Device.NewMTL4CommandQueue();
            _queue = new MetalQueue(this, commandQueue);
            _queue.AddResidencySet(_residencySet);

            _surface = new MetalSurface(this, window);
            _queue.SetSurface(_surface);

            _residencySet.Commit();
        }

        internal void BeginFrame()
        {
            var frameIndex = (int)(_frameCounter % MaxFramesInFlight);

            if (_frameCounter >= MaxFramesInFlight)
            {
                var waitValue = _frameCounter - MaxFramesInFlight + 1;
                WaitForEvent(waitValue);
            }

            _allocatorPool[frameIndex].Reset();
            CommandBuffer.BeginCommandBuffer(_allocatorPool[frameIndex]);
        }

        internal void EndFrame()
        {
            _frameCounter++;
            _queue.SignalEvent(_frameEvent, _frameCounter);
        }

        internal void MakeResident(MTLBuffer buffer)
        {
            lock (_residencyLock)
            {
                _residencySet.AddAllocation(new MTLAllocation(buffer));
                _residencyDirty = true;
            }
        }

        internal void MakeResident(MTLTexture texture)
        {
            lock (_residencyLock)
            {
                _residencySet.AddAllocation(new MTLAllocation(texture));
                _residencyDirty = true;
            }
        }

        internal void RemoveResident(MTLTexture texture)
        {
            lock (_residencyLock)
            {
                _residencySet.RemoveAllocation(new MTLAllocation(texture));
                _residencyDirty = true;
            }
        }

        internal void RemoveResident(MTLBuffer buffer)
        {
            lock (_residencyLock)
            {
                _residencySet.RemoveAllocation(new MTLAllocation(buffer));
                _residencyDirty = true;
            }
        }

        internal void CommitResidency()
        {
            lock (_residencyLock)
            {
                if (!_residencyDirty) return;
                _residencySet.Commit();
                _residencyDirty = false;
            }
        }

        private void WaitForEvent(ulong value)
        {
            while (_frameEvent.SignaledValue < value)
            {
                Thread.SpinWait(100);
            }
        }

        public override SlSurface GetSurface() => _surface;
        public override SlQueue GetQueue() => _queue;

        public override SlCommandEncoder CreateCommandEncoder()
        {
            CommitResidency();
            return new MetalCommandEncoder(this);
        }

        public override SlBuffer<T> CreateBuffer<T>(Span<T> data, SlBufferUsage usage)
            => new MetalBuffer<T>(this, data, usage);

        public override SlBuffer<T> CreateBuffer<T>(SlBufferDescriptor descriptor)
            => new MetalBuffer<T>(this, descriptor);

        public override SlTexture CreateTexture(SlTextureDescriptor descriptor)
            => new MetalTexture(this, descriptor);

        public override SlSampler CreateSampler(SlSamplerDescriptor descriptor)
            => new MetalSampler(this, descriptor);

        public override SlShaderModule CreateShaderModule(SlShaderModuleDescriptor descriptor)
            => new MetalShaderModule(this, descriptor);

        public override SlRenderPipeline CreateRenderPipeline(SlRenderPipelineDescriptor descriptor)
            => new MetalRenderPipeline(this, descriptor);

        public override SlBindGroupLayout CreateBindGroupLayout(SlBindGroupLayoutDescriptor descriptor)
            => new MetalBindGroupLayout(descriptor);

        public override SlBindGroup CreateBindGroup(SlBindGroupDescriptor descriptor)
            => new MetalBindGroup(descriptor);

        public override void Dispose()
        {
            if (_frameCounter > 0)
                WaitForEvent(_frameCounter);

            _queue?.DisposeInternal();
            _surface?.Dispose();

            for (var i = 0; i < _allocatorPool.Length; i++)
            {
                if (_allocatorPool[i].NativePtr != IntPtr.Zero)
                    _allocatorPool[i].Dispose();
            }

            if (CommandBuffer.NativePtr != IntPtr.Zero)
                CommandBuffer.Dispose();
            if (_frameEvent.NativePtr != IntPtr.Zero)
                _frameEvent.Dispose();
            if (_residencySet.NativePtr != IntPtr.Zero)
                _residencySet.Dispose();
            if (Compiler.NativePtr != IntPtr.Zero)
                Compiler.Dispose();
            if (Device.NativePtr != IntPtr.Zero)
                Device.Dispose();
        }
    }
}
