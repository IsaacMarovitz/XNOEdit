using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalTexture : SlTexture
    {
        private readonly MetalDevice _device;
        private readonly bool _owned;
        private MTLTexture _handle;

        public static implicit operator MTLTexture(MetalTexture texture) => texture._handle;

        public override ulong GpuResourceId => _handle.GpuResourceID._impl;

        internal MetalTexture(MetalDevice device, MTLTexture handle, bool owned = true)
        {
            _device = device;
            _owned = owned;
            _handle = handle;
        }

        internal MetalTexture(MetalDevice device, SlTextureDescriptor descriptor)
        {
            _device = device;
            _owned = true;

            var metalDescriptor = new MTLTextureDescriptor();
            metalDescriptor.Width = descriptor.Size.Width;
            metalDescriptor.Height = descriptor.Size.Height;
            metalDescriptor.Depth = descriptor.Size.DepthOrArrayLayers == 0 ? 1 : descriptor.Size.DepthOrArrayLayers;
            metalDescriptor.MipmapLevelCount = descriptor.MipLevelCount == 0 ? 1 : descriptor.MipLevelCount;
            metalDescriptor.SampleCount = descriptor.SampleCount == 0 ? 1 : descriptor.SampleCount;
            metalDescriptor.TextureType = descriptor.Dimension.Convert();
            metalDescriptor.PixelFormat = descriptor.Format.Convert();
            metalDescriptor.Usage = descriptor.Usage.Convert();
            metalDescriptor.StorageMode = descriptor.Usage.HasFlag(SlTextureUsage.CopyDst)
                ? MTLStorageMode.Shared
                : MTLStorageMode.Private;

            _handle = _device.Device.NewTexture(metalDescriptor);
            metalDescriptor.Dispose();

            // Make resident at creation — removed on dispose
            device.MakeResident(_handle);
        }

        public override SlTextureView CreateTextureView(SlTextureViewDescriptor descriptor)
        {
            return new MetalTextureView(_device, _handle, descriptor);
        }

        public override void Dispose()
        {
            if (_handle.NativePtr != IntPtr.Zero && _owned)
            {
                _device.RemoveResident(_handle);
                _handle.SetPurgeableState(MTLPurgeableState.Empty);
                _handle.Dispose();
            }
        }
    }
}
