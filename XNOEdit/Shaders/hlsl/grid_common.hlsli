#pragma once

#include "solaris_common.hlsli"

struct PushConstants
{
    uint ConstantOffset;
};

[[vk::push_constant]] ConstantBuffer<PushConstants> g_PushConstants : register(b0, space5);

struct Interpolators
{
    float4 Position : SV_Position;
    float3 Color    : COLOR;
    float  Distance : TEXCOORD0;
};
