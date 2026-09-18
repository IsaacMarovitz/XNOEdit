#pragma once

Texture2D<float4>      g_Texture2DHeap[]      : register(t0, space0);
Texture2DArray<float4> g_Texture2DArrayHeap[] : register(t0, space1);
TextureCube<float4>    g_TextureCubeHeap[]    : register(t0, space2);
SamplerState           g_SamplerHeap[]        : register(s0, space3);

StructuredBuffer<float4> g_Constants : register(t0, space4);

#define SL_NULL_TEXTURE_2D       0
#define SL_NULL_TEXTURE_2D_ARRAY 1
#define SL_NULL_TEXTURE_CUBE     2
#define SL_WHITE_TEXTURE_2D      3

float4x4 SlLoadMatrix(uint element)
{
    return float4x4(g_Constants[element + 0], g_Constants[element + 1],
                    g_Constants[element + 2], g_Constants[element + 3]);
}

float4 SlSample2D(uint textureIndex, uint samplerIndex, float2 uv)
{
    return g_Texture2DHeap[textureIndex].Sample(g_SamplerHeap[samplerIndex], uv);
}
