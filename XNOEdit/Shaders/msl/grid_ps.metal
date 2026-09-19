#include "grid_common.metali"

[[fragment]]
float4 shaderMain(GridInterpolators input [[stage_in]],
                  constant SlConstantHeap& g_ConstantHeap [[buffer(4)]],
                  constant GridPushConstants& g_PushConstants [[buffer(8)]])
{
    device const float4* constants = g_ConstantHeap.constants;
    uint base = g_PushConstants.ConstantOffset;

    float fadeStart = constants[base + 12].w;
    float fadeEnd   = constants[base + 13].x;

    float alpha = 1.0 - smoothstep(fadeStart, fadeEnd, input.Distance);
    return float4(input.Color, alpha);
}
