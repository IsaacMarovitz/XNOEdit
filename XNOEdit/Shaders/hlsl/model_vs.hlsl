#include "model_common.hlsli"

void shaderMain(
    in float3 position : POSITION,
    in float3 normal   : NORMAL,
    in float4 color    : COLOR,
    in float2 uv0      : TEXCOORD0,
    in float2 uv1      : TEXCOORD1,
    out Interpolators output)
{
    uint base = g_PushConstants.PerFrameOffset;

    float4x4 model      = SlLoadMatrix(base + SL_PF_MODEL);
    float4x4 view       = SlLoadMatrix(base + SL_PF_VIEW);
    float4x4 projection = SlLoadMatrix(base + SL_PF_PROJECTION);
    float vertColorStrength = g_Constants[base + SL_PF_CAMERA].w;

    float4 worldPos = mul(float4(position, 1.0), model);
    output.WorldPosition = worldPos.xyz;

    float3x3 normalMatrix = float3x3(model[0].xyz, model[1].xyz, model[2].xyz);
    output.Normal = normalize(mul(normal, normalMatrix));

    float3x3 tbn = SlCalculateTangentSpace(output.Normal);
    output.Tangent = tbn[0];
    output.Bitangent = tbn[1];

    output.UV0 = uv0;
    output.UV1 = uv1;
    output.Color = float4(lerp(float3(1.0, 1.0, 1.0), color.rgb, vertColorStrength), color.a);
    output.Position = mul(mul(worldPos, view), projection);
}
