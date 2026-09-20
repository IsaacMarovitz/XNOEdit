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
        public Vector3 SunDirection;
        public Vector3 SunColor;
        public Vector3 Ambient;

        public Vector4 MaterialDiffuse;
        public Vector4 MaterialAmbient;
        public Vector4 MaterialSpecular;
        public Vector4 MaterialEmission;

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
            in GuestMeshState state, in GuestSceneState scene, ReadOnlySpan<SlTextureIndex> stages)
        {
            _pixelConstants.Clear();

            var guestSunDirection = Vector3.Normalize(-scene.SunDirection);

            _pixelConstants.Set(GuestPixelRegisters.MaterialDiffuse, scene.MaterialDiffuse);
            _pixelConstants.Set(GuestPixelRegisters.MaterialAmbient, scene.MaterialAmbient);
            _pixelConstants.Set(GuestPixelRegisters.MaterialSpecular, scene.MaterialSpecular);
            _pixelConstants.Set(GuestPixelRegisters.MaterialEmission, scene.MaterialEmission);

            _pixelConstants.Set(GuestPixelRegisters.LightMiscAmbient, scene.Ambient, 1.0f);
            _pixelConstants.Set(GuestPixelRegisters.LightMiscEyePos, scene.CameraPosition, 1.0f);
            _pixelConstants.Set(GuestPixelRegisters.DirectionalLights + 0, guestSunDirection);
            _pixelConstants.Set(GuestPixelRegisters.DirectionalLights + 1, scene.SunColor, 1.0f);

            _pixelConstants.Set(GuestPixelRegisters.DirectionalLights + 2, guestSunDirection);
            _pixelConstants.Set(GuestPixelRegisters.DirectionalLights + 3, Vector4.Zero);

            for (var i = 0u; i < 2; i++)
            {
                var light = GuestPixelRegisters.PointLights + i * 3;

                _pixelConstants.Set(light + 0, Vector4.Zero);
                _pixelConstants.Set(light + 1, Vector4.Zero);
                _pixelConstants.Set(light + 2, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
            }

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

            // s11 is g_smpCSM; white means unshadowed.
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

            var view = scene.View;
            var projection = scene.Projection;

            // The vertex shaders declare g_Material at c0, which is the same numbering
            // the pixel shaders use — hence the shared register constants here.
            _vertexConstants.Set(GuestPixelRegisters.MaterialDiffuse, scene.MaterialDiffuse);
            _vertexConstants.Set(GuestPixelRegisters.MaterialAmbient, scene.MaterialAmbient);
            _vertexConstants.Set(GuestPixelRegisters.MaterialSpecular, scene.MaterialSpecular);
            _vertexConstants.Set(GuestPixelRegisters.MaterialEmission, scene.MaterialEmission);

            _vertexConstants.SetMatrix(GuestVertexRegisters.MatW, world, rows: 3);
            _vertexConstants.SetMatrix(GuestVertexRegisters.MatWv, world * view, rows: 3);
            _vertexConstants.SetMatrix(GuestVertexRegisters.MatWvp, world * view * projection);
            _vertexConstants.SetMatrix(GuestVertexRegisters.MatV, view, rows: 3);
            _vertexConstants.SetMatrix(GuestVertexRegisters.MatVp, view * projection);
            _vertexConstants.SetMatrix(GuestVertexRegisters.MatP, projection);

            Matrix4x4.Invert(view, out var inverseView);
            _vertexConstants.SetMatrix(GuestVertexRegisters.MatVi, inverseView, rows: 3);

            for (var i = 0u; i < 2; i++)
            {
                var light = GuestVertexRegisters.PointLights + i * 3;

                _vertexConstants.Set(light + 0, Vector4.Zero);
                _vertexConstants.Set(light + 1, Vector4.Zero);
                _vertexConstants.Set(light + 2, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
            }

            _vertexConstants.Set(GuestVertexRegisters.Ols + 0, scene.SunColor, 1.0f);
            _vertexConstants.Set(GuestVertexRegisters.Ols + 1, Vector4.Zero);
            _vertexConstants.Set(GuestVertexRegisters.Ols + 2, Vector4.Zero);
            _vertexConstants.Set(GuestVertexRegisters.Ols + 3, Vector4.Zero);
            _vertexConstants.Set(GuestVertexRegisters.Ols + 4, Vector4.Zero);
            _vertexConstants.Set(GuestVertexRegisters.Ols + 5, new Vector4(0.0f, 1.0f, 1.0f, 1.0f));

            _vertexConstants.SetMatrix(GuestVertexRegisters.CsmViewProj, Matrix4x4.Identity);

            _vertexConstants.Set(GuestVertexRegisters.LightMiscAmbient, scene.Ambient, 1.0f);
            _vertexConstants.Set(GuestVertexRegisters.LightMiscEyePos, scene.CameraPosition, 1.0f);
            _vertexConstants.Set(GuestVertexRegisters.LightMiscEyeVec,
                Vector3.Normalize(new Vector3(-inverseView.M31, -inverseView.M32, -inverseView.M33)));

            var guestSunDirection = Vector3.Normalize(-scene.SunDirection);

            _vertexConstants.Set(GuestVertexRegisters.DirectionalLights + 0, guestSunDirection);
            _vertexConstants.Set(GuestVertexRegisters.DirectionalLights + 1, scene.SunColor, 1.0f);

            // g_OffsetUV is (u0,v0,u1,v1) then (u2,v2,u3,v3) — one scroll offset per stage.
            _vertexConstants.Set(GuestVertexRegisters.OffsetUv + 0,
                new Vector4(offsets[0].X, offsets[0].Y, offsets[1].X, offsets[1].Y));
            _vertexConstants.Set(GuestVertexRegisters.OffsetUv + 1,
                new Vector4(offsets[2].X, offsets[2].Y, offsets[3].X, offsets[3].Y));

            var descriptors = new GuestRootDescriptors
            {
                VertexConstants = ctx.Ring.Write(_vertexConstants.Registers).DeviceAddress,
                PixelConstants = binding.PixelConstants,
                SharedConstants = binding.SharedConstants,
            };

            ctx.PushConstants(in descriptors);
        }
    }
}
