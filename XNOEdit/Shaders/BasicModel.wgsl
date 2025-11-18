// Uniform bindings
struct Uniforms {
    model: mat4x4<f32>,
    view: mat4x4<f32>,
    projection: mat4x4<f32>,
    lightDir: vec3<f32>,
    _pad1: f32,
    lightColor: vec3<f32>,
    _pad2: f32,
    viewPos: vec3<f32>,
    vertColorStrength: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

// Vertex input
struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) color: vec4<f32>,
    @location(3) uv: vec2<f32>
}

// Vertex output / Fragment input
struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) fragPos: vec3<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) color: vec4<f32>,
    @location(3) uv: vec2<f32>
}

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;

    let worldPos = uniforms.model * vec4<f32>(in.position, 1.0);
    out.fragPos = worldPos.xyz;

    // Transform normal to world space
    let normalMatrix = mat3x3<f32>(
        uniforms.model[0].xyz,
        uniforms.model[1].xyz,
        uniforms.model[2].xyz
    );

    out.normal = normalMatrix * in.normal;
    out.uv = in.uv;
    out.color = mix(vec4<f32>(1.0), in.color, uniforms.vertColorStrength);
    out.position = uniforms.projection * uniforms.view * worldPos;

    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // Desaturate light color for more subtle influence
    let desaturatedLight = mix(vec3<f32>(1.0), uniforms.lightColor, 0.3);

    // Ambient
    let ambientStrength = 0.3;
    let ambient = ambientStrength * desaturatedLight;

    // Diffuse
    let norm = normalize(in.normal);
    let lightDir = normalize(uniforms.lightDir);
    let diff = max(dot(norm, lightDir), 0.0);
    let diffuse = diff * desaturatedLight;

    // Specular - keep mostly white for realistic highlights
    let specularStrength = 0.5;
    let viewDir = normalize(uniforms.viewPos - in.fragPos);
    let reflectDir = reflect(-lightDir, norm);
    let spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
    let specular = specularStrength * spec * vec3<f32>(1.0);

    // Combine lighting with vertex color
    let result = (ambient + diffuse + specular) * in.color.rgb;
    return vec4<f32>(result, in.color.a);
}
