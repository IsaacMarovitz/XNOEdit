#pragma once

#include "solaris_common.hlsli"

struct PushConstants
{
    float4x4 InverseViewProjection;
    float4 CameraPosition;
    float4 SunDirection;
    float4 SunColor;
};

[[vk::push_constant]] ConstantBuffer<PushConstants> g_PushConstants : register(b0, space5);

struct Interpolators
{
    float4 Position : SV_Position;
    float3 ViewDir  : TEXCOORD0;
};
