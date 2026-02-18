#include <metal_stdlib>
using namespace metal;

struct SkyboxVertex {
    packed_float3 position;
};

struct Uniforms {
    float4x4      view;
    float4x4      projection;
    packed_float3  sunDirection;
    float          _pad1;
    packed_float3  sunColor;
    float          _pad2;
};

struct VertexOut {
    float4 position [[position]];
    float3 viewDir;
};

static float4x4 inverse4x4(float4x4 m) {
    float a00=m[0][0],a01=m[0][1],a02=m[0][2],a03=m[0][3];
    float a10=m[1][0],a11=m[1][1],a12=m[1][2],a13=m[1][3];
    float a20=m[2][0],a21=m[2][1],a22=m[2][2],a23=m[2][3];
    float a30=m[3][0],a31=m[3][1],a32=m[3][2],a33=m[3][3];
    float b00=a00*a11-a01*a10,b01=a00*a12-a02*a10,b02=a00*a13-a03*a10;
    float b03=a01*a12-a02*a11,b04=a01*a13-a03*a11,b05=a02*a13-a03*a12;
    float b06=a20*a31-a21*a30,b07=a20*a32-a22*a30,b08=a20*a33-a23*a30;
    float b09=a21*a32-a22*a31,b10=a21*a33-a23*a31,b11=a22*a33-a23*a32;
    float invDet=1.0/(b00*b11-b01*b10+b02*b09+b03*b08-b04*b07+b05*b06);
    return float4x4(
        float4((a11*b11-a12*b10+a13*b09)*invDet,(a02*b10-a01*b11-a03*b09)*invDet,(a31*b05-a32*b04+a33*b03)*invDet,(a22*b04-a21*b05-a23*b03)*invDet),
        float4((a12*b08-a10*b11-a13*b07)*invDet,(a00*b11-a02*b08+a03*b07)*invDet,(a32*b02-a30*b05-a33*b01)*invDet,(a20*b05-a22*b02+a23*b01)*invDet),
        float4((a10*b10-a11*b08+a13*b06)*invDet,(a01*b08-a00*b10-a03*b06)*invDet,(a30*b04-a31*b02+a33*b00)*invDet,(a21*b02-a20*b04-a23*b00)*invDet),
        float4((a11*b07-a10*b09-a12*b06)*invDet,(a00*b09-a01*b07+a02*b06)*invDet,(a31*b01-a30*b03-a32*b00)*invDet,(a20*b03-a21*b01+a22*b00)*invDet));
}

vertex VertexOut vs_main(
    device const SkyboxVertex* vertexData [[buffer(0)]],
    device const Uniforms* u [[buffer(4)]],
    uint vid [[vertex_id]])
{
    float3 pos = float3(vertexData[vid].position);

    VertexOut out;
    float4x4 invProj = inverse4x4(u->projection);
    float4x4 invView = inverse4x4(u->view);
    float4 clipPos = float4(pos.xy, 1.0, 1.0);
    float4 viewPos = invProj * clipPos;
    viewPos = viewPos / viewPos.w;
    float4 worldPos = invView * viewPos;
    out.viewDir = normalize(worldPos.xyz - invView[3].xyz);
    out.position = float4(pos, 1.0);
    return out;
}

static float3 atmosphere(float3 dir, float3 sunDir, float3 sunCol) {
    float sunDot = max(dot(dir, sunDir), 0.0);
    float horizon = abs(dir.y);
    float horizonFalloff = pow(1.0 - horizon, 1.5);
    float horizonTint = pow(1.0 - horizon, 2.0);
    float3 zenith = mix(float3(0.25,0.45,0.75), sunCol*0.6, horizonTint*0.3);
    float3 horizonSky = mix(float3(0.7,0.8,0.95), sunCol, horizonTint*0.7);
    float3 color;
    if (dir.y > 0.0) {
        color = mix(horizonSky, zenith, pow(dir.y, 0.7));
        color += sunCol * pow(sunDot, 8.0) * 0.6;
        color += sunCol * pow(sunDot, 256.0) * 1.5;
        color = mix(color, sunCol, pow(sunDot, 3.0) * horizonTint * 0.4);
    } else {
        color = mix(float3(0.5,0.55,0.6), float3(0.3,0.35,0.4), pow(-dir.y, 0.5));
        if (sunDot > 0.0) color += sunCol * 0.4 * pow(sunDot, 4.0) * 0.15;
    }
    color = mix(color, mix(float3(0.75,0.8,0.9), sunCol, 0.5), pow(horizonFalloff, 2.0)*0.3);
    return color;
}

fragment float4 fs_main(
    VertexOut in [[stage_in]],
    device const Uniforms* u [[buffer(4)]])
{
    return float4(atmosphere(normalize(in.viewDir), float3(u->sunDirection), float3(u->sunColor)), 1.0);
}
