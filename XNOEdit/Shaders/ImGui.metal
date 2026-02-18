#include <metal_stdlib>
using namespace metal;

// Matches C# ImDrawVert: {Vector2 Pos, Vector2 Uv, uint Col} = 20 bytes
struct ImDrawVert {
    packed_float2 position;  // 8 bytes
    packed_float2 uv;        // 8 bytes
    uint          col;       // 4 bytes (RGBA8 packed)
};

struct Uniforms {
    float4x4 mvp;
};

struct VertexOut {
    float4 position [[position]];
    float4 color;
    float2 uv;
};

// Unpack RGBA8 uint to float4
static float4 unpackColor(uint c) {
    return float4(
        float(c & 0xFF) / 255.0,
        float((c >> 8) & 0xFF) / 255.0,
        float((c >> 16) & 0xFF) / 255.0,
        float((c >> 24) & 0xFF) / 255.0
    );
}

vertex VertexOut vs_main(
    device const ImDrawVert* vertexData [[buffer(0)]],
    device const Uniforms* uniforms [[buffer(4)]],
    uint vid [[vertex_id]])
{
    device const ImDrawVert& v = vertexData[vid];

    VertexOut out;
    out.position = uniforms->mvp * float4(float2(v.position), 0.0, 1.0);
    out.color = unpackColor(v.col);
    out.uv = float2(v.uv);
    return out;
}

fragment float4 fs_main(
    VertexOut in [[stage_in]],
    sampler s [[sampler(1)]],
    texture2d<float> t [[texture(8)]])
{
    return in.color * t.sample(s, in.uv);
}
