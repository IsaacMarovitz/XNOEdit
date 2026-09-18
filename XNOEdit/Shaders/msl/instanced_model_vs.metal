#include "model_common.metali"

struct InstancedVertexStageIn
{
    float3 position [[attribute(0)]];
    float3 normal   [[attribute(1)]];
    float4 color    [[attribute(2)]];
    float2 uv0      [[attribute(3)]];
    float2 uv1      [[attribute(4)]];
    float4 row0     [[attribute(5)]];
    float4 row1     [[attribute(6)]];
    float4 row2     [[attribute(7)]];
    float4 row3     [[attribute(8)]];
};

[[vertex]]
ModelInterpolators shaderMain(InstancedVertexStageIn input [[stage_in]],
                              constant SlConstantHeap& g_ConstantHeap [[buffer(4)]],
                              constant ModelPushConstants& g_PushConstants [[buffer(8)]])
{
    device const float4* constants = g_ConstantHeap.constants;
    uint base = g_PushConstants.PerFrameOffset;

    float4x4 view       = SlLoadMatrix(constants, base + SL_PF_VIEW);
    float4x4 projection = SlLoadMatrix(constants, base + SL_PF_PROJECTION);
    float vertColorStrength = constants[base + SL_PF_CAMERA].w;

    float4x4 instanceTransform = float4x4(input.row0, input.row1, input.row2, input.row3);

    float4 worldPos = instanceTransform * float4(input.position, 1.0);

    ModelInterpolators output;
    output.WorldPosition = worldPos.xyz;
    output.Normal = normalize((instanceTransform * float4(input.normal, 0.0)).xyz);

    float3x3 tbn = SlCalculateTangentSpace(output.Normal);
    output.Tangent = tbn[0];
    output.Bitangent = tbn[1];

    output.UV0 = input.uv0;
    output.UV1 = input.uv1;
    output.Color = mix(float4(1.0), input.color, vertColorStrength);
    output.Position = projection * view * worldPos;

    return output;
}
