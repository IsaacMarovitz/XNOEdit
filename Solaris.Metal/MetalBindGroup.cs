using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    /// <summary>
    /// Stores resource references to be applied to argument tables when SetBindGroup is called.
    ///
    /// Each entry holds the data needed for bindless binding:
    /// - Buffers: GPU address (+ offset)
    /// - Textures: GPU resource ID
    /// - Samplers: sampler state reference
    /// </summary>
    [SupportedOSPlatform("macos")]
    internal class MetalBindGroup : SlBindGroup
    {
        internal readonly BindlessResource[] Resources;

        internal unsafe MetalBindGroup(SlBindGroupDescriptor descriptor)
        {
            var layout = (MetalBindGroupLayout)descriptor.Layout;
            var entries = descriptor.Entries ?? [];
            Resources = new BindlessResource[entries.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                ref var entry = ref entries[i];
                ref var layoutEntry = ref layout.Entries[i];

                Resources[i].Binding = entry.Binding;
                Resources[i].Type = layoutEntry.Type;
                Resources[i].Visibility = layoutEntry.Visibility;

                switch (layoutEntry.Type)
                {
                    case SlBindingType.Buffer:
                        if (entry.Buffer.HasValue)
                        {
                            var buf = entry.Buffer.Value;
                            Resources[i].BufferSource = buf.Source;
                            Resources[i].BufferOffset = buf.Offset;
                        }
                        break;

                    case SlBindingType.Texture:
                        if (entry.TextureView != null)
                        {
                            Resources[i].TextureResourceId = entry.TextureView.GpuResourceId != 0
                                ? new MTLResourceID { _impl = entry.TextureView.GpuResourceId }
                                : new MTLTexture((IntPtr)entry.TextureView.GetHandle()).GpuResourceID;
                        }
                        break;

                    case SlBindingType.Sampler:
                        if (entry.Sampler != null)
                        {
                            Resources[i].SamplerState = (MetalSampler)entry.Sampler;
                        }
                        break;
                }
            }
        }

        public override void Dispose() { }
    }

    internal struct BindlessResource
    {
        public uint Binding;
        public SlBindingType Type;
        public SlShaderStage Visibility;

        public ISlBuffer BufferSource;
        public ulong BufferOffset;

        public MTLResourceID TextureResourceId;
        public MTLSamplerState SamplerState;
    }
}
