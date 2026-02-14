using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Solaris.RHI;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuShaderModule : SlShaderModule
    {
        private readonly WgpuDevice _device;
        internal ShaderModule* Handle { get; }

        public WgpuShaderModule(WgpuDevice device, SlShaderModuleDescriptor descriptor)
        {
            _device = device;

            if (descriptor.Language != SlShaderLanguage.Wgsl)
                throw new NotSupportedException($"WebGPU backend only supports WGSL shaders, got {descriptor.Language}");

            var source = descriptor.Source as string
                         ?? throw new ArgumentException("WGSL shader source must be a string");

            var srcPtr = SilkMarshal.StringToPtr(source);
            var labelPtr = string.IsNullOrEmpty(descriptor.Label)
                ? IntPtr.Zero
                : SilkMarshal.StringToPtr(descriptor.Label);

            try
            {
                var wgslDesc = new ShaderModuleWGSLDescriptor
                {
                    Code = (byte*)srcPtr,
                    Chain = new ChainedStruct(sType: SType.ShaderModuleWgslDescriptor),
                };

                var desc = new ShaderModuleDescriptor
                {
                    Label = labelPtr != IntPtr.Zero ? (byte*)labelPtr : null,
                    NextInChain = (ChainedStruct*)(&wgslDesc),
                };

                Handle = _device.Wgpu.DeviceCreateShaderModule(_device, in desc);
            }
            finally
            {
                SilkMarshal.Free(srcPtr);
                if (labelPtr != IntPtr.Zero)
                    SilkMarshal.Free(labelPtr);
            }
        }

        public override void Dispose()
        {
            if (Handle != null)
                _device.Wgpu.ShaderModuleRelease(Handle);
        }
    }
}
