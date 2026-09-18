#include "grid_common.hlsli"

void shaderMain(in float3 position : POSITION, in float3 color : COLOR, out Interpolators output)
{
    uint base = g_PushConstants.ConstantOffset;

    float4x4 model      = SlLoadMatrix(base + 0);
    float4x4 view       = SlLoadMatrix(base + 4);
    float4x4 projection = SlLoadMatrix(base + 8);
    float3   cameraPos  = g_Constants[base + 12].xyz;

    float4 worldPos = mul(float4(position, 1.0), model);

    output.Color = color;
    output.Distance = length(worldPos.xyz - cameraPos);
    output.Position = mul(mul(worldPos, view), projection);
}
