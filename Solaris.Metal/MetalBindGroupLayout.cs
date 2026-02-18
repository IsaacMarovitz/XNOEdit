using System.Runtime.Versioning;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalBindGroupLayout : SlBindGroupLayout
    {
        internal SlBindGroupLayoutEntry[] Entries { get; }

        internal MetalBindGroupLayout(SlBindGroupLayoutDescriptor descriptor)
        {
            Entries = descriptor.Entries ?? [];
        }

        public override void Dispose() { }
    }
}
