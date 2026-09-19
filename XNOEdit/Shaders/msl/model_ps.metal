#include "model_common.metali"

#define WORLD_SPACE_NORMALS 0

[[fragment]]
float4 shaderMain(ModelInterpolators input [[stage_in]],
                  constant SlTexture2DHeap& g_Texture2DHeap [[buffer(0)]],
                  constant SlSamplerHeap& g_SamplerHeap [[buffer(3)]],
                  constant SlConstantHeap& g_ConstantHeap [[buffer(4)]],
                  constant ModelPushConstants& g_PushConstants [[buffer(8)]])
{
    device const float4* constants = g_ConstantHeap.constants;
    uint base = g_PushConstants.PerFrameOffset;
    uint samplerIndex = g_PushConstants.SamplerIndex;

    float4 sunDirection = constants[base + SL_PF_SUN_DIR];
    float3 sunColor     = constants[base + SL_PF_SUN_COLOR].rgb;
    float3 cameraPos    = constants[base + SL_PF_CAMERA].xyz;
    float  lightmapMix  = constants[base + SL_PF_LIGHTMAP].x;

    float4 mainColor  = SlSample2D(g_Texture2DHeap, g_SamplerHeap, g_PushConstants.MainTextureIndex, samplerIndex, input.UV0);
    float4 blendColor = SlSample2D(g_Texture2DHeap, g_SamplerHeap, g_PushConstants.BlendMapIndex, samplerIndex, input.UV0);
    float4 normalMap  = SlSample2D(g_Texture2DHeap, g_SamplerHeap, g_PushConstants.NormalMapIndex, samplerIndex, input.UV0);
    float4 lightmap   = SlSample2D(g_Texture2DHeap, g_SamplerHeap, g_PushConstants.LightMapIndex, samplerIndex, input.UV1);

    float4 textureColor = g_PushConstants.Blend == 1.0
        ? mix(blendColor, mainColor, input.Color.a)
        : mainColor;

    float4 baseDiffuse = textureColor * g_PushConstants.DiffuseColor;

    if (baseDiffuse.a < g_PushConstants.AlphaRef)
    {
        discard_fragment();
    }

    float3 worldNormal;

#if WORLD_SPACE_NORMALS
    worldNormal = normalize(normalMap.xyz * 2.0 - 1.0);
#else
    if (g_PushConstants.NormalMapIndex == SL_NULL_TEXTURE_2D)
    {
        worldNormal = normalize(input.Normal);
    }
    else
    {
        float3 tangentNormal = normalMap.xyz * 2.0 - 1.0;
        float3x3 tbn = float3x3(
            normalize(input.Tangent),
            normalize(input.Bitangent),
            normalize(input.Normal));
        worldNormal = normalize(tbn * tangentNormal);
    }
#endif

    float3 mainLightDir = normalize(sunDirection.xyz);
    float3 viewDir = normalize(cameraPos - input.WorldPosition);

    float3 ambient = g_PushConstants.AmbientColor.rgb * 0.3 * sunColor;

    float diff = max(dot(worldNormal, mainLightDir), 0.0);
    float3 diffuse = diff * sunColor;

    float3 specular = float3(0.0);

    if (g_PushConstants.Specular == 1.0)
    {
        float3 halfDir = normalize(mainLightDir + viewDir);
        float specPower = max(g_PushConstants.SpecularPower, 1.0);
        float spec = pow(max(dot(worldNormal, halfDir), 0.0), specPower);
        specular = spec * g_PushConstants.SpecularColor.rgb * textureColor.a;
    }

    float3 sceneLighting = ambient + diffuse;

    if (lightmapMix == 1.0)
    {
        sceneLighting *= lightmap.rgb * 0.5 + 0.5;
    }

    float3 litDiffuse = baseDiffuse.rgb * input.Color.rgb * sceneLighting;
    return float4(litDiffuse + specular, 1.0);
}
