using System.Numerics;
using System.Runtime.InteropServices;

namespace XNOEdit.Guest
{
    public static class GuestVertexRegisters
    {
        public const uint OffsetUv = 5;             // 2 registers
        public const uint Ols = 8;                  // 6: sun colour, Rayleigh/Mie
        public const uint RtDimensions = 14;
        public const uint ClipInfo = 15;
        public const uint KhronosParam = 16;
        public const uint LightMiscAmbient = 28;
        public const uint LightMiscEyePos = 29;
        public const uint LightMiscEyeVec = 30;
        public const uint DirectionalLights = 31;   // 2 lights x (direction, colour)
        public const uint PointLights = 39;         // 2 lights x (position, colour, attenuation)
        public const uint MatW = 64;                // 3 or 4
        public const uint MatWv = 68;               // 3
        public const uint MatWvp = 72;              // 4
        public const uint MatV = 76;                // 3
        public const uint MatVi = 80;               // 3 or 4
        public const uint MatP = 84;                // 4
        public const uint MatVp = 88;               // 4
        public const uint CsmViewProj = 92;         // 4
        public const uint BoneMatrices = 96;        // 32 x 3
        public const uint Morph = 192;
        public const uint MorphFlags = 193;
        public const uint SystemLightMapWork = 194;

        /// <summary>First per-effect local register; nothing below is shader specific.</summary>
        public const uint LocalsBase = 210;
    }

    public static class GuestPixelRegisters
    {
        public const uint MaterialDiffuse = 0;
        public const uint MaterialAmbient = 1;
        public const uint MaterialSpecular = 2;
        public const uint MaterialEmission = 3;
        public const uint Misc = 4;
        public const uint KhronosParam = 16;
        public const uint LightMiscAmbient = 28;
        public const uint LightMiscEyePos = 29;
        public const uint LightMiscEyeVec = 30;
        public const uint DirectionalLights = 31;
        public const uint PointLights = 39;

        public const uint LocalsBase = 210;
    }

    public sealed class GuestConstantFile
    {
        public const int RegisterCount = 256;

        private readonly Vector4[] _registers = new Vector4[RegisterCount];

        public ReadOnlySpan<Vector4> Registers => _registers;

        public void Clear() => Array.Clear(_registers);

        public void Set(uint register, in Vector4 value)
        {
            _registers[register] = value;
        }

        public void Set(uint register, in Vector3 value, float w = 0.0f)
        {
            _registers[register] = new Vector4(value, w);
        }

        /// <summary>
        /// Writes a matrix as <paramref name="rows"/> consecutive registers. Three is
        /// correct for affine transforms the engine only partially uploads; four for
        /// anything with a projective component.
        /// </summary>
        public void SetMatrix(uint register, in Matrix4x4 value, int rows = 4)
        {
            var t = Matrix4x4.Transpose(value);

            _registers[register + 0] = new Vector4(t.M11, t.M12, t.M13, t.M14);
            _registers[register + 1] = new Vector4(t.M21, t.M22, t.M23, t.M24);
            _registers[register + 2] = new Vector4(t.M31, t.M32, t.M33, t.M34);

            if (rows >= 4)
            {
                _registers[register + 3] = new Vector4(t.M41, t.M42, t.M43, t.M44);
            }
        }

        public ReadOnlySpan<byte> AsBytes() => MemoryMarshal.AsBytes<Vector4>(_registers);
    }

    /// <summary>
    /// The guest's shared constant block, bound at root descriptor slot 2. Layout must
    /// match SharedConstants in the recomp exactly, since the recompiled shaders index
    /// it directly.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GuestSharedConstants
    {
        public const int TextureSlots = 16;

        public unsafe fixed uint Texture2DIndices[TextureSlots];
        public unsafe fixed uint Texture2DArrayIndices[TextureSlots];
        public unsafe fixed uint TextureCubeIndices[TextureSlots];
        public unsafe fixed uint SamplerIndices[TextureSlots];

        public uint Booleans;
        public uint SwappedTexcoords;
        public uint SwappedNormals;
        public uint SwappedBinormals;
        public uint SwappedTangents;
        public uint SwappedBlendWeights;

        public float HalfPixelOffsetX;
        public float HalfPixelOffsetY;

        public Vector4 ClipPlane;

        public uint ClipPlaneEnabled;
        public float AlphaThreshold;
        public uint ConditionalSurveyIndex;
        public uint ConditionalRenderingIndex;
    }

    [Flags]
    public enum GuestSpecConstants : uint
    {
        None = 0,
        R11G11B10Normal = 1 << 0,
        AlphaTest = 1 << 1,
        ConditionalRendering = 1 << 5,
    }
}
