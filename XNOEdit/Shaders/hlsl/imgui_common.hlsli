#pragma once

#include "solaris_common.hlsli"

struct PushConstants
{
    float4x4 Mvp;
    uint TextureIndex;
    uint SamplerIndex;
};

[[vk::push_constant]] ConstantBuffer<PushConstants> g_PushConstants : register(b0, space5);

struct Interpolators
{
    float4 Position : SV_Position;
    float4 Color    : COLOR;
    float2 UV       : TEXCOORD0;
};
