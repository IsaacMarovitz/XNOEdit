#include "imgui_common.metali"

[[fragment]]
float4 shaderMain(ImGuiInterpolators input [[stage_in]],
                  constant SlTexture2DHeap& g_Texture2DHeap [[buffer(0)]],
                  constant SlSamplerHeap& g_SamplerHeap [[buffer(3)]],
                  constant ImGuiPushConstants& g_PushConstants [[buffer(8)]])
{
    return input.Color * SlSample2D(g_Texture2DHeap, g_SamplerHeap,
                                    g_PushConstants.TextureIndex, g_PushConstants.SamplerIndex, input.UV);
}
