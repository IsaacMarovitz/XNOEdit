using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal unsafe class MetalTextureView : SlTextureView
    {
        private readonly MetalDevice _device;
        private MTLTexture _handle;
        private readonly bool _owned;

        public static implicit operator MTLTexture(MetalTextureView view) => view._handle;

        public override ulong GpuResourceId => _handle.GpuResourceID._impl;

        internal MetalTextureView(MetalDevice device, MTLTexture parent, SlTextureViewDescriptor descriptor)
        {
            _device = device;
            _owned = true;
            _handle = parent.NewTextureView(
                descriptor.Format.Convert(),
                EnumConversion.Convert(descriptor.Dimension),
                new NSRange { location = descriptor.BaseMipLevel, length =  descriptor.MipLevelCount },
                new NSRange { location = descriptor.BaseArrayLayer, length =  descriptor.ArrayLayerCount });
        }

        internal MetalTextureView(MetalDevice device, MTLTexture handle, bool owned = false)
        {
            _device = device;
            _owned = owned;
            _handle = handle;
        }

        public override void* GetHandle() => (void*)_handle.NativePtr;

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
