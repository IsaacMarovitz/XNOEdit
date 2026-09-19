#pragma once

#include "solaris_common.hlsli"

struct PushConstants
{
    // Per-frame region, offset 0.
    uint PerFrameOffset;
    uint SamplerIndex;
    uint2 Padding;

    // Per-mesh region, offset 16.
    float4 AmbientColor;
    float4 DiffuseColor;
    float4 SpecularColor;
    float4 EmissiveColor;
    float SpecularPower;
    float AlphaRef;
    float Alpha;
    float Blend;
    float Specular;
    uint MainTextureIndex;
    uint BlendMapIndex;
    uint NormalMapIndex;
    uint LightMapIndex;
};

[[vk::push_constant]] ConstantBuffer<PushConstants> g_PushConstants : register(b0, space5);

#define SL_PF_MODEL      0
#define SL_PF_VIEW       4
#define SL_PF_PROJECTION 8
#define SL_PF_SUN_DIR    12
#define SL_PF_SUN_COLOR  13
#define SL_PF_CAMERA     14
#define SL_PF_LIGHTMAP   15

struct Interpolators
{
    float4 Position      : SV_Position;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal        : NORMAL;
    float3 Tangent       : TANGENT;
    float3 Bitangent     : BINORMAL;
    float4 Color         : COLOR;
    float2 UV0           : TEXCOORD1;
    float2 UV1           : TEXCOORD2;
};

float3x3 SlCalculateTangentSpace(float3 normal)
{
    float3 tangent;

    if (abs(normal.y) < 0.999)
    {
        tangent = normalize(cross(float3(0.0, 1.0, 0.0), normal));
    }
    else
    {
        tangent = normalize(cross(float3(1.0, 0.0, 0.0), normal));
    }

    float3 bitangent = cross(normal, tangent);
    return float3x3(tangent, bitangent, normal);
}
