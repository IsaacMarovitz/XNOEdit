#include "skybox_common.hlsli"

float3 Atmosphere(float3 dir, float3 sunDirection, float3 sunColor)
{
    float sunDot = max(dot(dir, sunDirection), 0.0);

    float horizon = abs(dir.y);
    float horizonFalloff = pow(1.0 - horizon, 1.5);

    const float3 baseZenithColor = float3(0.25, 0.45, 0.75);
    const float3 baseHorizonSkyColor = float3(0.7, 0.8, 0.95);
    const float3 horizonGroundColor = float3(0.5, 0.55, 0.6);
    const float3 nadirColor = float3(0.3, 0.35, 0.4);

    float horizonTint = pow(1.0 - horizon, 2.0);
    float3 zenithColor = lerp(baseZenithColor, sunColor * 0.6, horizonTint * 0.3);
    float3 horizonSkyColor = lerp(baseHorizonSkyColor, sunColor, horizonTint * 0.7);

    float3 color;

    if (dir.y > 0.0)
    {
        float skyBlend = pow(dir.y, 0.7);
        color = lerp(horizonSkyColor, zenithColor, skyBlend);

        float sunGlow = pow(sunDot, 8.0) * 0.6;
        float sunCore = pow(sunDot, 256.0) * 1.5;

        color += sunColor * sunGlow;
        color += sunColor * sunCore;

        float sunScatter = pow(sunDot, 3.0) * horizonTint * 0.4;
        color = lerp(color, sunColor, sunScatter);
    }
    else
    {
        float groundBlend = pow(-dir.y, 0.5);
        color = lerp(horizonGroundColor, nadirColor, groundBlend);

        if (sunDot > 0.0)
        {
            float groundGlow = pow(sunDot, 4.0) * 0.15;
            color += sunColor * 0.4 * groundGlow;
        }
    }

    float fogAmount = pow(horizonFalloff, 2.0) * 0.3;
    float3 fogColor = lerp(float3(0.75, 0.8, 0.9), sunColor, 0.5);
    color = lerp(color, fogColor, fogAmount);

    return color;
}

float4 shaderMain(in Interpolators input) : SV_Target
{
    float3 dir = normalize(input.ViewDir);
    float3 color = Atmosphere(dir, g_PushConstants.SunDirection.xyz, g_PushConstants.SunColor.rgb);
    return float4(color, 1.0);
}
