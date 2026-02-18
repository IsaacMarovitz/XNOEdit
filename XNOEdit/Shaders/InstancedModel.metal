#include <metal_stdlib>
using namespace metal;

// Same vertex layout as Model.metal
struct ModelVertex {
    packed_float3 position;
    packed_float3 normal;
    packed_float4 color;
    packed_float2 uv0;
    packed_float2 uv1;
};

// Instance data: 4x4 float matrix (64 bytes)
struct InstanceData {
    float4x4 transform;
};

struct PerFrameUniforms {
    float4x4      model;
    float4x4      view;
    float4x4      projection;
    float4         sunDirection;
    float4         sunColor;
    packed_float3  cameraPosition;
    float          vertColorStrength;
    float          lightmap;
};

struct PerMeshUniforms {
    float4 ambientColor;
    float4 diffuseColor;
    float4 specularColor;
    float4 emissiveColor;
    float  specularPower;
    float  alphaRef;
    float  alpha;
    float  blend;
    float  specular;
};

struct VertexOut {
    float4 position       [[position]];
    float3 world_position;
    float3 normal;
    float3 tangent;
    float3 bitangent;
    float4 color;
    float2 uv0;
    float2 uv1;
};

static float3x3 calculateTangentSpace(float3 n) {
    float3 t = abs(n.y) < 0.999
        ? normalize(cross(float3(0,1,0), n))
        : normalize(cross(float3(1,0,0), n));
    return float3x3(t, cross(n, t), n);
}

vertex VertexOut vs_main(
    device const ModelVertex* vertexData [[buffer(0)]],
    device const InstanceData* instanceData [[buffer(1)]],
    device const PerFrameUniforms* pf [[buffer(4)]],
    uint vid [[vertex_id]],
    uint iid [[instance_id]])
{
    device const ModelVertex& v = vertexData[vid];
    float4x4 inst = instanceData[iid].transform;

    float3 pos = float3(v.position);
    float4 worldPos = inst * float4(pos, 1.0);
    float3 worldNormal = normalize((inst * float4(float3(v.normal), 0.0)).xyz);

    VertexOut out;
    out.position = pf->projection * pf->view * worldPos;
    out.world_position = worldPos.xyz;
    out.normal = worldNormal;

    float3x3 tbn = calculateTangentSpace(worldNormal);
    out.tangent = tbn[0];
    out.bitangent = tbn[1];

    out.uv0 = float2(v.uv0);
    out.uv1 = float2(v.uv1);
    out.color = mix(float4(1.0), float4(v.color), pf->vertColorStrength);
    return out;
}

constant bool WORLD_SPACE_NORMALS = false;

fragment float4 fs_main(
    VertexOut in [[stage_in]],
    device const PerFrameUniforms* pf [[buffer(4)]],
    device const PerMeshUniforms* pm [[buffer(8)]],
    sampler tex_sampler [[sampler(8)]],
    texture2d<float> main_texture [[texture(17)]],
    texture2d<float> blend_texture [[texture(18)]],
    texture2d<float> normal_texture [[texture(19)]],
    texture2d<float> lightmap_texture [[texture(20)]])
{
    float4 mainColor = main_texture.sample(tex_sampler, in.uv0);
    float4 blendColor = blend_texture.sample(tex_sampler, in.uv0);
    float4 normalMap = normal_texture.sample(tex_sampler, in.uv0);
    float4 lm = lightmap_texture.sample(tex_sampler, in.uv1);

    float4 textureColor = (pm->blend == 1.0) ? mix(blendColor, mainColor, in.color.a) : mainColor;
    float4 baseDiffuse = textureColor * pm->diffuseColor;
    if (baseDiffuse.a < pm->alphaRef) discard_fragment();

    float3 worldNormal;
    if (WORLD_SPACE_NORMALS) {
        worldNormal = normalize(normalMap.xyz * 2.0 - 1.0);
    } else {
        float3x3 tbn = float3x3(normalize(in.tangent), normalize(in.bitangent), normalize(in.normal));
        worldNormal = normalize(tbn * (normalMap.xyz * 2.0 - 1.0));
    }

    float3 camPos = float3(pf->cameraPosition);
    float3 lightDir = normalize(pf->sunDirection.xyz);
    float3 viewDir = normalize(camPos - in.world_position);
    float3 ambient = pm->ambientColor.rgb * 0.3 * pf->sunColor.rgb;
    float3 diffuse = max(dot(worldNormal, lightDir), 0.0) * pf->sunColor.rgb;

    float3 specular = float3(0.0);
    if (pm->specular == 1.0) {
        float3 halfDir = normalize(lightDir + viewDir);
        float spec = pow(max(dot(worldNormal, halfDir), 0.0), max(pm->specularPower, 1.0));
        specular = spec * pm->specularColor.rgb * textureColor.a;
    }

    float3 sceneLighting = ambient + diffuse;
    if (pf->lightmap == 1.0) sceneLighting *= lm.rgb * 0.5 + 0.5;

    return float4(baseDiffuse.rgb * in.color.rgb * sceneLighting + specular, 1.0);
}
