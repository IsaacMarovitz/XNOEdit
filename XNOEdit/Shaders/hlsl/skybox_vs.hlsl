#include "skybox_common.hlsli"

void shaderMain(in float3 position : POSITION, out Interpolators output)
{
    float4 clipPos = float4(position.xy, 1.0, 1.0);

    float4 worldPos = mul(g_PushConstants.InverseViewProjection, clipPos);
    worldPos /= worldPos.w;

    output.ViewDir = normalize(worldPos.xyz);
    output.Position = float4(position, 1.0);
}
