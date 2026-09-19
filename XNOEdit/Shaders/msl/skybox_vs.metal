#include "skybox_common.metali"

struct SkyboxVertexStageIn
{
    float3 position [[attribute(0)]];
};

[[vertex]]
SkyboxInterpolators shaderMain(SkyboxVertexStageIn input [[stage_in]],
                               constant SkyboxPushConstants& g_PushConstants [[buffer(8)]])
{
    // Full-screen triangle strip already in clip space; depth at the far plane.
    float4 clipPos = float4(input.position.xy, 1.0, 1.0);

    float4 worldPos = g_PushConstants.InverseViewProjection * clipPos;
    worldPos /= worldPos.w;

    SkyboxInterpolators output;
    output.ViewDir = normalize(worldPos.xyz);
    output.Position = float4(input.position, 1.0);

    return output;
}
