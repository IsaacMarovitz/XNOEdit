#include "grid_common.hlsli"

float4 shaderMain(in Interpolators input) : SV_Target
{
    uint base = g_PushConstants.ConstantOffset;

    float fadeStart = g_Constants[base + 12].w;
    float fadeEnd   = g_Constants[base + 13].x;

    float alpha = 1.0 - smoothstep(fadeStart, fadeEnd, input.Distance);
    return float4(input.Color, alpha);
}
