struct Uniforms {
    model: mat4x4<f32>,
    view: mat4x4<f32>,
    projection: mat4x4<f32>,
    cameraPos: vec3<f32>,
    fadeStart: f32,
    fadeEnd: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) color: vec3<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) color: vec3<f32>,
    @location(1) distance: f32,
}

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;

    let worldPos = uniforms.model * vec4<f32>(in.position, 1.0);
    out.color = in.color;
    out.distance = length(worldPos.xyz - uniforms.cameraPos);
    out.position = uniforms.projection * uniforms.view * worldPos;

    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // Distance-based fading
    let alpha = 1.0 - smoothstep(uniforms.fadeStart, uniforms.fadeEnd, in.distance);
    return vec4<f32>(in.color, alpha);
}
