using Plume;
using Solaris;
using XNOEdit.Logging;

namespace XNOEdit.Guest
{
    public sealed class GuestMaterial : IDisposable
    {
        public const uint VertexStride = 96;

        private GuestMaterial(
            SlDevice device,
            SlMaterial material,
            string name)
        {
            Material = material;
            IsSky = IsSkyEffect(name);

            Sampler = device.GetSampler(SlSamplerDescriptor.LinearWrap with { AnisotropyEnabled = true, MaxLod = 32.0f });
        }

        public SlMaterial Material { get; }

        public SlSamplerIndex Sampler { get; }

        public bool IsSky { get; }

        private static bool IsSkyEffect(string name)
        {
            var effect = name.AsSpan()[(name.LastIndexOf('/') + 1)..];

            return effect.StartsWith("Sky", StringComparison.Ordinal)
                   || effect.StartsWith("EndSky", StringComparison.Ordinal);
        }

        public static GuestMaterial? Create(SlDevice device, GuestShaderCache cache, string name, byte[] fxo)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(cache);

            var containers = GuestShaderContainer.Scan(fxo);

            var vertexContainer = containers.FirstOrDefault(c => c.Stage == GuestShaderStage.Vertex);
            var pixelContainer = containers.FirstOrDefault(c => c.Stage == GuestShaderStage.Pixel);

            if (vertexContainer == null || pixelContainer == null)
            {
                Logger.Warning?.PrintMsg(LogClass.Application,
                    $"'{name}' has {containers.Count} shader container(s); expected one vertex and one pixel");
                return null;
            }

            if (!cache.TryGet(vertexContainer.Hash, out var vertexEntry) ||
                !cache.TryGet(pixelContainer.Hash, out var pixelEntry))
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"'{name}' is not in the recompiled shader cache");
                return null;
            }

            var vertexBytecode = cache.GetBytecode(vertexEntry);
            var pixelBytecode = cache.GetBytecode(pixelEntry);

            if (vertexBytecode.IsEmpty || pixelBytecode.IsEmpty)
            {
                Logger.Warning?.PrintMsg(LogClass.Application, $"'{name}' has no bytecode for this backend");
                return null;
            }

            var vertexShader = SlShader.Create(device, vertexBytecode, "shaderMain");
            var pixelShader = SlShader.Create(device, pixelBytecode, "shaderMain");

            var material = new SlMaterial(device, vertexShader, pixelShader, CreateVertexLayout(), name);

            return new GuestMaterial(
                device,
                material,
                name);
        }

        public SlPipelineVariant Variant(GuestDrawBucket bucket, in GuestMeshState state, bool cull)
        {
            return new SlPipelineVariant
            {
                Topology = RenderPrimitiveTopology.TriangleList,
                CullMode = cull && bucket != GuestDrawBucket.PunchThrough
                    ? RenderCullMode.Back
                    : RenderCullMode.None,
                FrontFace = RenderFrontFace.CounterClockwise,
                DepthWrite = !IsSky && state.DepthWrite,
                DepthTest = IsSky || state.DepthTest,
                DepthCompare = IsSky ? RenderComparisonFunction.GreaterEqual : state.DepthCompare,
                Blend = bucket == GuestDrawBucket.Transparent && state.BlendEnabled ? state.Blend : null,
                SpecConstants = state.AlphaTest ? (uint)GuestSpecConstants.AlphaTest : 0,
            };
        }

        private static SlVertexLayout CreateVertexLayout() => new(
            new SlVertexBufferLayout(0, VertexStride, SlVertexStepMode.Vertex,
            [
                new SlVertexAttribute("POSITION", 0, 0, RenderFormat.R32G32B32Float, 0),
                new SlVertexAttribute("NORMAL", 0, 4, RenderFormat.R32G32B32Float, 12),
                new SlVertexAttribute("TANGENT", 0, 8, RenderFormat.R32G32B32Float, 24),
                new SlVertexAttribute("BINORMAL", 0, 12, RenderFormat.R32G32B32Float, 36),
                new SlVertexAttribute("COLOR", 0, 17, RenderFormat.R32G32B32A32Float, 48),
                new SlVertexAttribute("TEXCOORD", 0, 13, RenderFormat.R32G32B32A32Float, 64),
                new SlVertexAttribute("TEXCOORD", 1, 14, RenderFormat.R32G32B32A32Float, 80),
            ]));

        public void Dispose()
        {
            Material.Dispose();
        }
    }
}
