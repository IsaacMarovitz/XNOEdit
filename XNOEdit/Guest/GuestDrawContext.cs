using System.Numerics;
using System.Runtime.InteropServices;
using Solaris;
using Solaris.Graph;

namespace XNOEdit.Guest
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GuestRootDescriptors
    {
        public ulong VertexConstants;
        public ulong PixelConstants;
        public ulong SharedConstants;
    }

    public struct GuestSceneState
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;

        public Vector3 CameraPosition;
        public Vector4 Ambient;
        public SceneLight Main;
        public SceneLight Sub;
        public SceneOls Ols;
        public SlTextureIndex EnvMap;

        public Vector4 MaterialDiffuse;
        public Vector4 MaterialAmbient;
        public Vector4 MaterialSpecular;
        public Vector4 MaterialEmission;
        public float MaterialPower;

        public float AlphaThreshold;
        public bool CullBackfaces;
    }

    public readonly record struct GuestBinding(ulong PixelConstants, ulong SharedConstants);

    public sealed class GuestDrawContext
    {
        private readonly GuestConstantFile _vertexConstants = new();
        private readonly GuestConstantFile _pixelConstants = new();

        public unsafe GuestBinding BindMaterial(
            SlPassContext ctx, GuestMaterial material, GuestDrawBucket bucket,
            in GuestMeshState state, in GuestSceneState scene,
            ReadOnlySpan<SlTextureIndex> stages, ReadOnlySpan<Vector2> offsets)
        {
            _pixelConstants.Clear();
            WriteShared(_pixelConstants, in scene, offsets);

            var shared = new GuestSharedConstants
            {
                AlphaThreshold = scene.AlphaThreshold,
            };

            for (var i = 0; i < GuestSharedConstants.TextureSlots; i++)
            {
                shared.Texture2DIndices[i] = SlTextureIndex.NullTexture2D.Packed;
                shared.Texture2DArrayIndices[i] = SlTextureIndex.NullTexture2DArray.Packed;
                shared.TextureCubeIndices[i] = SlTextureIndex.NullTextureCube.Packed;
                shared.SamplerIndices[i] = material.Sampler.Slot;
            }

            for (var i = 0; i < stages.Length && i < GuestSharedConstants.TextureSlots; i++)
            {
                shared.Texture2DIndices[i] = stages[i].Packed;
            }

            // s8 is g_smpEnvMap and s11 is g_smpCSM; white means unshadowed.
            shared.TextureCubeIndices[8] = scene.EnvMap.Packed;
            shared.Texture2DArrayIndices[11] = SlTextureIndex.WhiteTexture2DArray.Packed;

            var variant = material.Variant(bucket, in state, scene.CullBackfaces);
            ctx.SetPipeline(material.Material.Pipeline(in variant, ctx.Signature));

            return new GuestBinding(
                ctx.Ring.Write(_pixelConstants.Registers).DeviceAddress,
                ctx.Ring.Write(in shared).DeviceAddress);
        }

        public void BindInstance(
            SlPassContext ctx, in GuestBinding binding,
            in GuestSceneState scene, in Matrix4x4 world, ReadOnlySpan<Vector2> offsets)
        {
            _vertexConstants.Clear();
            WriteShared(_vertexConstants, in scene, offsets);

            var view = scene.View;
            var projection = scene.Projection;

            _vertexConstants.SetMatrix(GuestRegisters.MatW, world, rows: 3);
            _vertexConstants.SetMatrix(GuestRegisters.MatWv, world * view, rows: 3);
            _vertexConstants.SetMatrix(GuestRegisters.MatWvp, world * view * projection);
            _vertexConstants.SetMatrix(GuestRegisters.MatV, view, rows: 3);
            _vertexConstants.SetMatrix(GuestRegisters.MatVp, view * projection);
            _vertexConstants.SetMatrix(GuestRegisters.MatP, projection);

            Matrix4x4.Invert(view, out var inverseView);
            _vertexConstants.SetMatrix(GuestRegisters.MatVi, inverseView, rows: 3);

            _vertexConstants.SetMatrix(GuestRegisters.CsmViewProj, Matrix4x4.Identity);

            // Only the vertex stage derives the eye vector; it comes from the camera
            // rather than from anything the pixel shaders can see.
            _vertexConstants.Set(GuestRegisters.LightMiscEyeVec,
                Vector3.Normalize(new Vector3(-inverseView.M31, -inverseView.M32, -inverseView.M33)));

            var descriptors = new GuestRootDescriptors
            {
                VertexConstants = ctx.Ring.Write(_vertexConstants.Registers).DeviceAddress,
                PixelConstants = binding.PixelConstants,
                SharedConstants = binding.SharedConstants,
            };

            ctx.PushConstants(in descriptors);
        }

        private static void WriteShared(
            GuestConstantFile file, in GuestSceneState scene, ReadOnlySpan<Vector2> offsets)
        {
            file.Set(GuestRegisters.MaterialDiffuse, scene.MaterialDiffuse);
            file.Set(GuestRegisters.MaterialAmbient, scene.MaterialAmbient);
            file.Set(GuestRegisters.MaterialSpecular, scene.MaterialSpecular);
            file.Set(GuestRegisters.MaterialEmission, scene.MaterialEmission);

            file.Set(GuestRegisters.Misc, new Vector4(0.0f, 0.0f, 0.0f, scene.MaterialPower));

            file.Set(GuestRegisters.LightMiscAmbient, Intensity(scene.Ambient));
            file.Set(GuestRegisters.LightMiscEyePos, scene.CameraPosition, 1.0f);

            file.Set(GuestRegisters.DirectionalLights + 0, ToGuestDirection(scene.Main));
            file.Set(GuestRegisters.DirectionalLights + 1, Intensity(scene.Main.Color));
            file.Set(GuestRegisters.DirectionalLights + 2, ToGuestDirection(scene.Sub));
            file.Set(GuestRegisters.DirectionalLights + 3, Intensity(scene.Sub.Color));

            // Attenuation of (1, 0, 0) keeps the shader's reciprocal finite; the
            // colours are black, so these contribute nothing.
            for (var i = 0u; i < 2; i++)
            {
                var light = GuestRegisters.PointLights + i * 3;

                file.Set(light + 0, Vector4.Zero);
                file.Set(light + 1, Vector4.Zero);
                file.Set(light + 2, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
            }

            var g = scene.Ols.G;

            file.Set(GuestRegisters.Ols + 0, Coefficient(scene.Ols.SunColor));
            file.Set(GuestRegisters.Ols + 1, Coefficient(scene.Ols.BRay));
            file.Set(GuestRegisters.Ols + 2, Coefficient(scene.Ols.BMie));
            file.Set(GuestRegisters.Ols + 3, Weighted(scene.Ols.BRay, 3.0f / (16.0f * MathF.PI)));
            file.Set(GuestRegisters.Ols + 4, Weighted(scene.Ols.BMie, 1.0f / (4.0f * MathF.PI)));
            file.Set(GuestRegisters.Ols + 5,
                new Vector4((1.0f - g) * (1.0f - g), 1.0f + g * g, -2.0f * g, 0.0f));

            // g_OffsetUV is (u0,v0,u1,v1) then (u2,v2,u3,v3) — one scroll offset per stage.
            file.Set(GuestRegisters.OffsetUv + 0,
                new Vector4(offsets[0].X, offsets[0].Y, offsets[1].X, offsets[1].Y));
            file.Set(GuestRegisters.OffsetUv + 1,
                new Vector4(offsets[2].X, offsets[2].Y, offsets[3].X, offsets[3].Y));
        }

        private static Vector4 Intensity(Vector4 color) =>
            new(color.X * color.W, color.Y * color.W, color.Z * color.W, 0.0f);

        private static Vector4 Coefficient(Vector4 value) =>
            new(value.X * value.W, value.Y * value.W, value.Z * value.W, 1.0f);

        private static Vector4 Weighted(Vector4 value, float weight) =>
            new(value.X * value.W * weight, value.Y * value.W * weight, value.Z * value.W * weight, weight);

        private static Vector4 ToGuestDirection(in SceneLight light)
        {
            var direction = Vector3.Normalize(light.Target - light.Position);

            return new Vector4(direction.X, direction.Z, -direction.Y, 0.0f);
        }
    }
}
