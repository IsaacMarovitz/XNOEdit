#include "model_common.hlsli"

void shaderMain(
    in float3 position : POSITION,
    in float3 normal   : NORMAL,
    in float4 color    : COLOR,
    in float2 uv0      : TEXCOORD0,
    in float2 uv1      : TEXCOORD1,
    in float4 row0     : INSTANCE0,
    in float4 row1     : INSTANCE1,
    in float4 row2     : INSTANCE2,
    in float4 row3     : INSTANCE3,
    out Interpolators output)
{
    uint base = g_PushConstants.PerFrameOffset;

    float4x4 view       = SlLoadMatrix(base + SL_PF_VIEW);
    float4x4 projection = SlLoadMatrix(base + SL_PF_PROJECTION);
    float vertColorStrength = g_Constants[base + SL_PF_CAMERA].w;

    float4x4 instanceTransform = float4x4(row0, row1, row2, row3);

    float4 worldPos = mul(float4(position, 1.0), instanceTransform);
    output.WorldPosition = worldPos.xyz;

    output.Normal = normalize(mul(float4(normal, 0.0), instanceTransform).xyz);

    float3x3 tbn = SlCalculateTangentSpace(output.Normal);
    output.Tangent = tbn[0];
    output.Bitangent = tbn[1];

    output.UV0 = uv0;
    output.UV1 = uv1;
    output.Color = lerp(float4(1.0, 1.0, 1.0, 1.0), color, vertColorStrength);
    output.Position = mul(mul(worldPos, view), projection);
}
