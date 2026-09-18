#include "imgui_common.metali"

struct ImGuiVertexStageIn
{
    float2 position [[attribute(0)]];
    float2 uv       [[attribute(1)]];
    float4 color    [[attribute(2)]];
};

[[vertex]]
ImGuiInterpolators shaderMain(ImGuiVertexStageIn input [[stage_in]],
                              constant ImGuiPushConstants& g_PushConstants [[buffer(8)]])
{
    ImGuiInterpolators output;
    output.Position = g_PushConstants.Mvp * float4(input.position, 0.0, 1.0);
    output.Color = input.color;
    output.UV = input.uv;

    return output;
}
