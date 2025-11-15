struct Uniforms {
    view: mat4x4<f32>,
    projection: mat4x4<f32>,
    sunDirection: vec3<f32>,
    _pad1: f32,
    sunColor: vec3<f32>,
    _pad2: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
    @location(0) position: vec3<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) viewDir: vec3<f32>,
}

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out: VertexOutput;

    // Calculate view direction by unprojecting the NDC position
    let invProj = inverse(uniforms.projection);
    let invView = inverse(uniforms.view);

    let clipPos = vec4<f32>(in.position.xy, 1.0, 1.0);
    var viewPos = invProj * clipPos;
    viewPos = viewPos / viewPos.w;

    let worldPos = invView * viewPos;
    let cameraPos = invView[3].xyz;

    out.viewDir = normalize(worldPos.xyz - cameraPos);
    out.position = vec4<f32>(in.position, 1.0);

    return out;
}

fn atmosphere(dir: vec3<f32>) -> vec3<f32> {
    let sunDot = max(dot(dir, uniforms.sunDirection), 0.0);

    // Improved sky-ground blending
    let horizon = abs(dir.y);
    let horizonFalloff = pow(1.0 - horizon, 1.5);

    // Base color palette
    let baseZenithColor = vec3<f32>(0.25, 0.45, 0.75);     // Deep blue sky
    let baseHorizonSkyColor = vec3<f32>(0.7, 0.8, 0.95);   // Light blue-white at horizon
    let horizonGroundColor = vec3<f32>(0.5, 0.55, 0.6);    // Warm gray ground horizon
    let nadirColor = vec3<f32>(0.3, 0.35, 0.4);            // Dark ground color

    // Tint sky colors based on sun color
    let horizonTint = pow(1.0 - horizon, 2.0);  // Stronger at horizon
    let zenithColor = mix(baseZenithColor, uniforms.sunColor * 0.6, horizonTint * 0.3);
    let horizonSkyColor = mix(baseHorizonSkyColor, uniforms.sunColor, horizonTint * 0.7);

    var color: vec3<f32>;

    if (dir.y > 0.0) {
        // Sky - blend from zenith to horizon
        let skyBlend = pow(dir.y, 0.7);
        color = mix(horizonSkyColor, zenithColor, skyBlend);

        // Sun glow in sky with custom color
        let sunGlow = pow(sunDot, 8.0) * 0.6;
        let sunCore = pow(sunDot, 256.0) * 1.5;

        // Apply sun color to glow and core
        color += uniforms.sunColor * sunGlow;
        color += uniforms.sunColor * sunCore;

        // Additional atmospheric scattering around sun
        let sunScatter = pow(sunDot, 3.0) * horizonTint * 0.4;
        color = mix(color, uniforms.sunColor, sunScatter);
    } else {
        // Ground - blend from horizon down to nadir
        let groundBlend = pow(-dir.y, 0.5);
        color = mix(horizonGroundColor, nadirColor, groundBlend);

        // Subtle ambient bounce light from 'sun' below horizon
        if (sunDot > 0.0) {
            let groundGlow = pow(sunDot, 4.0) * 0.15;
            color += uniforms.sunColor * 0.4 * groundGlow;
        }
    }

    // Atmospheric fog near horizon - tinted by sun
    let fogAmount = pow(horizonFalloff, 2.0) * 0.3;
    let fogColor = mix(vec3<f32>(0.75, 0.8, 0.9), uniforms.sunColor, 0.5);
    color = mix(color, fogColor, fogAmount);

    return color;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    let dir = normalize(in.viewDir);
    let color = atmosphere(dir);
    return vec4<f32>(color, 1.0);
}

fn inverse(m: mat4x4<f32>) -> mat4x4<f32> {
    let a00 = m[0][0]; let a01 = m[0][1]; let a02 = m[0][2]; let a03 = m[0][3];
    let a10 = m[1][0]; let a11 = m[1][1]; let a12 = m[1][2]; let a13 = m[1][3];
    let a20 = m[2][0]; let a21 = m[2][1]; let a22 = m[2][2]; let a23 = m[2][3];
    let a30 = m[3][0]; let a31 = m[3][1]; let a32 = m[3][2]; let a33 = m[3][3];

    let b00 = a00 * a11 - a01 * a10;
    let b01 = a00 * a12 - a02 * a10;
    let b02 = a00 * a13 - a03 * a10;
    let b03 = a01 * a12 - a02 * a11;
    let b04 = a01 * a13 - a03 * a11;
    let b05 = a02 * a13 - a03 * a12;
    let b06 = a20 * a31 - a21 * a30;
    let b07 = a20 * a32 - a22 * a30;
    let b08 = a20 * a33 - a23 * a30;
    let b09 = a21 * a32 - a22 * a31;
    let b10 = a21 * a33 - a23 * a31;
    let b11 = a22 * a33 - a23 * a32;

    let det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
    let invDet = 1.0 / det;

    return mat4x4<f32>(
        vec4<f32>(
            (a11 * b11 - a12 * b10 + a13 * b09) * invDet,
            (a02 * b10 - a01 * b11 - a03 * b09) * invDet,
            (a31 * b05 - a32 * b04 + a33 * b03) * invDet,
            (a22 * b04 - a21 * b05 - a23 * b03) * invDet
        ),
        vec4<f32>(
            (a12 * b08 - a10 * b11 - a13 * b07) * invDet,
            (a00 * b11 - a02 * b08 + a03 * b07) * invDet,
            (a32 * b02 - a30 * b05 - a33 * b01) * invDet,
            (a20 * b05 - a22 * b02 + a23 * b01) * invDet
        ),
        vec4<f32>(
            (a10 * b10 - a11 * b08 + a13 * b06) * invDet,
            (a01 * b08 - a00 * b10 - a03 * b06) * invDet,
            (a30 * b04 - a31 * b02 + a33 * b00) * invDet,
            (a21 * b02 - a20 * b04 - a23 * b00) * invDet
        ),
        vec4<f32>(
            (a11 * b07 - a10 * b09 - a12 * b06) * invDet,
            (a00 * b09 - a01 * b07 + a02 * b06) * invDet,
            (a31 * b01 - a30 * b03 - a32 * b00) * invDet,
            (a20 * b03 - a21 * b01 + a22 * b00) * invDet
        )
    );
}
