#include <metal_stdlib>
using namespace metal;

// Matches C# grid vertex: {Vector3 position, Vector3 color} = 24 bytes
struct GridVertex {
    packed_float3 position;  // 12 bytes
    packed_float3 color;     // 12 bytes
};

struct Uniforms {
    float4x4      model;
    float4x4      view;
    float4x4      projection;
    packed_float3  cameraPos;
    float          fadeStart;
    float          fadeEnd;
};

struct VertexOut {
    float4 position [[position]];
    float3 color;
    float  distance;
};

vertex VertexOut vs_main(
    device const GridVertex* vertexData [[buffer(0)]],
    device const Uniforms* uniforms [[buffer(4)]],
    uint vid [[vertex_id]])
{
    device const GridVertex& v = vertexData[vid];

    VertexOut out;
    float4 worldPos = uniforms->model * float4(float3(v.position), 1.0);
    out.color = float3(v.color);
    out.distance = length(worldPos.xyz - float3(uniforms->cameraPos));
    out.position = uniforms->projection * uniforms->view * worldPos;
    return out;
}

fragment float4 fs_main(
    VertexOut in [[stage_in]],
    device const Uniforms* uniforms [[buffer(4)]])
{
    float alpha = 1.0 - smoothstep(uniforms->fadeStart, uniforms->fadeEnd, in.distance);
    return float4(in.color, alpha);
}
