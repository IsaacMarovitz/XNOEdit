using System.Numerics;
using System.Runtime.InteropServices;
using Plume;
using Solaris;

namespace XNOEdit.Renderer.Shaders
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PerFrameUniforms
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector4 SunDirection;
        public Vector4 SunColor;
        public Vector3 CameraPosition;
        public float VertColorStrength;
        public float Lightmap;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ModelPerFramePush
    {
        public uint PerFrameOffset;
        public uint SamplerIndex;
    }

    public static class ModelPushConstants
    {
        public const uint PerFrameOffset = 0;
        public const uint PerMeshOffset = 16;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PerMeshConstants
    {
        public Vector4 AmbientColor;
        public Vector4 DiffuseColor;
        public Vector4 SpecularColor;
        public Vector4 EmissiveColor;
        public float SpecularPower;
        public float AlphaRef;
        public float Alpha;
        public float Blend;
        public float Specular;

        public uint MainTextureIndex;
        public uint BlendMapIndex;
        public uint NormalMapIndex;
        public uint LightMapIndex;
    }

    public class ModelShader : ShaderModule
    {
        public const uint VertexStride = 56;

        private static readonly Dictionary<string, SlPipelineVariant> Variants = new()
        {
            ["default"] = new()
            {
                Topology = RenderPrimitiveTopology.TriangleList,
                CullMode = RenderCullMode.None,
                FrontFace = RenderFrontFace.CounterClockwise,
                DepthWrite = true,
                DepthCompare = RenderComparisonFunction.Greater,
                AlphaBlend = true
            },
            ["culled"] = new()
            {
                Topology = RenderPrimitiveTopology.TriangleList,
                CullMode = RenderCullMode.Back,
                FrontFace = RenderFrontFace.CounterClockwise,
                DepthWrite = true,
                DepthCompare = RenderComparisonFunction.Greater,
                AlphaBlend = true
            }
        };

        public ModelShader(SlDevice device, ReadOnlySpan<byte> vertex, ReadOnlySpan<byte> pixel, string label = "Model Shader")
            : base(device, vertex, pixel, label, Variants)
        {
            Sampler = device.GetSampler(SlSamplerDescriptor.LinearWrap with { AnisotropyEnabled = true, MaxLod = 32.0f });
        }

        public SlSamplerIndex Sampler { get; }

        protected override SlVertexLayout CreateVertexLayout() => new(
            new SlVertexBufferLayout(0, VertexStride, SlVertexStepMode.Vertex,
            [
                new SlVertexAttribute("POSITION", 0, 0, RenderFormat.R32G32B32Float, 0),
                new SlVertexAttribute("NORMAL", 0, 1, RenderFormat.R32G32B32Float, 12),
                new SlVertexAttribute("COLOR", 0, 2, RenderFormat.R32G32B32A32Float, 24),
                new SlVertexAttribute("TEXCOORD", 0, 3, RenderFormat.R32G32Float, 40),
                new SlVertexAttribute("TEXCOORD", 1, 4, RenderFormat.R32G32Float, 48),
            ]));
    }
}
