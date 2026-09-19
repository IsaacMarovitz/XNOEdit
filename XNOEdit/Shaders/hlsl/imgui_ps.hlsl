#include "imgui_common.hlsli"

float4 shaderMain(in Interpolators input) : SV_Target
{
    return input.Color * SlSample2D(g_PushConstants.TextureIndex, g_PushConstants.SamplerIndex, input.UV);
}
