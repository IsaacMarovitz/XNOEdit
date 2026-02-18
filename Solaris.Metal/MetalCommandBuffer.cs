using System.Runtime.Versioning;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalCommandBuffer : SlCommandBuffer
    {
        internal readonly MetalDevice Device;

        internal MetalCommandBuffer(MetalDevice device)
        {
            Device = device;
        }

        public override void Dispose() { }
    }
}
