#include "grid_common.metali"

struct GridVertexStageIn
{
    float3 position [[attribute(0)]];
    float3 color    [[attribute(1)]];
};

[[vertex]]
GridInterpolators shaderMain(GridVertexStageIn input [[stage_in]],
                             constant SlConstantHeap& g_ConstantHeap [[buffer(4)]],
                             constant GridPushConstants& g_PushConstants [[buffer(8)]])
{
    device const float4* constants = g_ConstantHeap.constants;
    uint base = g_PushConstants.ConstantOffset;

    float4x4 model      = SlLoadMatrix(constants, base + 0);
    float4x4 view       = SlLoadMatrix(constants, base + 4);
    float4x4 projection = SlLoadMatrix(constants, base + 8);
    float3   cameraPos  = constants[base + 12].xyz;

    float4 worldPos = model * float4(input.position, 1.0);

    GridInterpolators output;
    output.Color = input.color;
    output.Distance = length(worldPos.xyz - cameraPos);
    output.Position = projection * view * worldPos;

    return output;
}
