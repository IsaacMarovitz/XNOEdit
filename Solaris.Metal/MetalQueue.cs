using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal unsafe class MetalQueue : SlQueue
    {
        private readonly MetalDevice _device;
        private MTL4CommandQueue _handle;
        private MetalSurface? _surface;

        public static implicit operator MTL4CommandQueue(MetalQueue queue) => queue._handle;

        internal MetalQueue(MetalDevice device, MTL4CommandQueue handle)
        {
            _device = device;
            _handle = handle;
        }

        internal void SetSurface(MetalSurface surface) => _surface = surface;

        internal void AddResidencySet(MTLResidencySet residencySet)
        {
            _handle.AddResidencySet(residencySet);
        }

        internal void SignalEvent(MTLSharedEvent evt, ulong value)
        {
            _handle.SignalEvent(evt, value);
        }

        public override void WriteTexture(SlCopyTextureDescriptor descriptor, Span<byte> data, SlTextureDataLayout layout, SlExtent3D extent)
        {
            fixed (byte* ptr = data)
            {
                WriteTexture(descriptor, ptr, (UIntPtr)data.Length, layout, extent);
            }
        }

        public override void WriteTexture(SlCopyTextureDescriptor descriptor, byte* data, UIntPtr size, SlTextureDataLayout layout, SlExtent3D extent)
        {
            var texture = (MetalTexture)descriptor.Texture;
            MTLTexture mtlTexture = texture;

            var region = new MTLRegion
            {
                origin = new MTLOrigin
                {
                    x = descriptor.Origin.X,
                    y = descriptor.Origin.Y,
                    z = descriptor.Origin.Z
                },
                size = new MTLSize
                {
                    width = extent.Width,
                    height = extent.Height,
                    depth = extent.DepthOrArrayLayers
                }
            };

            mtlTexture.ReplaceRegion(
                region,
                descriptor.MipLevel,
                (IntPtr)data,
                layout.BytesPerRow);
        }

        public override void Submit(SlCommandBuffer commandBuffer)
        {
            using var pool = new NSAutoreleasePool();

            // Flush any pending residency changes (e.g. mid-frame buffer creation)
            _device.CommitResidency();

            var cb = _device.CommandBuffer;
            var drawable = _surface?.CurrentDrawable;

            if (drawable.HasValue && drawable.Value.NativePtr != IntPtr.Zero)
            {
                _handle.Wait(drawable.Value);
                _handle.Commit([cb], 1);
                _handle.SignalDrawable(drawable.Value);
            }
            else
            {
                _handle.Commit([cb], 1);
            }

            _device.EndFrame();
        }

        /// <summary>
        /// No-op. The queue is owned by MetalDevice and shares its lifetime.
        /// User code (e.g. ModelMesh.CreateMeshUniforms) may call GetQueue() + Dispose(),
        /// which must not destroy the underlying MTL4CommandQueue.
        /// </summary>
        public override void Dispose() { }

        /// <summary>
        /// Actually releases the native handle. Only called by MetalDevice.Dispose().
        /// </summary>
        internal void DisposeInternal()
        {
            if (_handle.NativePtr != IntPtr.Zero)
                _handle.Dispose();
        }
    }
}
