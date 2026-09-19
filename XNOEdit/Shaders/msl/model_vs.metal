#include "model_common.metali"

[[vertex]]
ModelInterpolators shaderMain(ModelVertexStageIn input [[stage_in]],
                              constant SlConstantHeap& g_ConstantHeap [[buffer(4)]],
                              constant ModelPushConstants& g_PushConstants [[buffer(8)]])
{
    device const float4* constants = g_ConstantHeap.constants;
    uint base = g_PushConstants.PerFrameOffset;

    float4x4 model      = SlLoadMatrix(constants, base + SL_PF_MODEL);
    float4x4 view       = SlLoadMatrix(constants, base + SL_PF_VIEW);
    float4x4 projection = SlLoadMatrix(constants, base + SL_PF_PROJECTION);
    float vertColorStrength = constants[base + SL_PF_CAMERA].w;

    float4 worldPos = model * float4(input.position, 1.0);

    ModelInterpolators output;
    output.WorldPosition = worldPos.xyz;

    float3x3 normalMatrix = float3x3(model[0].xyz, model[1].xyz, model[2].xyz);
    output.Normal = normalize(normalMatrix * input.normal);

    float3x3 tbn = SlCalculateTangentSpace(output.Normal);
    output.Tangent = tbn[0];
    output.Bitangent = tbn[1];

    output.UV0 = input.uv0;
    output.UV1 = input.uv1;
    output.Color = float4(mix(float3(1.0), input.color.rgb, vertColorStrength), input.color.a);
    output.Position = projection * view * worldPos;

    return output;
}
