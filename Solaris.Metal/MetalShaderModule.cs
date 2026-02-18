using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalShaderModule : SlShaderModule
    {
        internal MTLLibrary Library { get; }

        internal MetalShaderModule(MetalDevice device, SlShaderModuleDescriptor descriptor)
        {
            if (descriptor.Language != SlShaderLanguage.Msl)
                throw new NotSupportedException(
                    $"Metal backend only supports MSL shaders, got {descriptor.Language}");

            var source = descriptor.Source as string
                         ?? throw new ArgumentException("MSL shader source must be a string");

            using var options = new MTLCompileOptions();
            var error = new NSError(IntPtr.Zero);
            Library = device.Device.NewLibrary(source, options, ref error);

            if (Library.NativePtr == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to compile Metal shader '{descriptor.Label}': {error.LocalizedDescription}");
        }

        public override void Dispose()
        {
            if (Library.NativePtr != IntPtr.Zero)
                Library.Dispose();
        }
    }
}
