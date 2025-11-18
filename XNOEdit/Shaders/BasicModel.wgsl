// Uniform bindings
struct Uniforms {
    model: mat4x4<f32>,
    view: mat4x4<f32>,
    projection: mat4x4<f32>,

    ambientColor: vec4<f32>,
    diffuseColor: vec4<f32>,
    specularColor: vec4<f32>,
    emissiveColor: vec4<f32>,

    lightDir: vec3<f32>,
    _pad1: f32,
    lightColor: vec3<f32>,
    _pad2: f32,
    viewPos: vec3<f32>,
    vertColorStrength: f32,
    specularPower: f32
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

// Group 1: Textures (matching your binding layout)
@group(1) @binding(0) var mainSampler: sampler;
@group(1) @binding(1) var mainTexture: texture_2d<f32>;
@group(1) @binding(2) var blendTexture: texture_2d<f32>;
@group(1) @binding(3) var normalTexture: texture_2d<f32>;
@group(1) @binding(4) var lightmapTexture: texture_2d<f32>;

const WORLD_SPACE_NORMALS: i32 = 0;

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
    @location(2) tangent: vec3<f32>,
    @location(3) bitangent: vec3<f32>,
    @location(4) color: vec4<f32>,
    @location(5) uv: vec2<f32>
}

// Calculate tangent space for normal mapping
fn calculateTangentSpace(normal: vec3<f32>) -> mat3x3<f32> {
    var tangent: vec3<f32>;
    if (abs(normal.y) < 0.999) {
        tangent = normalize(cross(vec3<f32>(0.0, 1.0, 0.0), normal));
    } else {
        tangent = normalize(cross(vec3<f32>(1.0, 0.0, 0.0), normal));
    }
    let bitangent = cross(normal, tangent);
    return mat3x3<f32>(tangent, bitangent, normal);
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

    out.normal = normalize(normalMatrix * in.normal);

    // Calculate tangent space for normal mapping
    let tbn = calculateTangentSpace(out.normal);
    out.tangent = tbn[0];
    out.bitangent = tbn[1];

    out.uv = in.uv;
    out.color = mix(vec4<f32>(1.0), in.color, uniforms.vertColorStrength);
    out.position = uniforms.projection * uniforms.view * worldPos;

    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // Sample textures
    let mainColor = textureSample(mainTexture, mainSampler, in.uv);
    let blendColor = textureSample(blendTexture, mainSampler, in.uv);
    let normalMap = textureSample(normalTexture, mainSampler, in.uv);
    let lightmap = textureSample(lightmapTexture, mainSampler, in.uv);

    // Blend main and blend texture using vertex alpha
    let textureColor = mix(blendColor, mainColor, in.color.a);

    // Apply material diffuse color to texture
    let baseDiffuse = textureColor * uniforms.diffuseColor;

    // Normal mapping
    var worldNormal: vec3<f32>;

    if (WORLD_SPACE_NORMALS == 1) {
        // World-space normal map - use directly
        worldNormal = normalize(normalMap.xyz * 2.0 - 1.0);
    } else {
        // Tangent-space normal map - transform to world space
        let tangentNormal = normalMap.xyz * 2.0 - 1.0;
        let tbn = mat3x3<f32>(
            normalize(in.tangent),
            normalize(in.bitangent),
            normalize(in.normal)
        );
        worldNormal = normalize(tbn * tangentNormal);
    }

    // === Lighting Setup ===
    let mainLightDir = normalize(uniforms.lightDir);
    let viewDir = normalize(uniforms.viewPos - in.fragPos);

    // === Ambient ===
    // Material ambient color * scene ambient
    let ambient = uniforms.ambientColor.rgb * 0.3 * uniforms.lightColor;

    // === Diffuse ===
    let diff = max(dot(worldNormal, mainLightDir), 0.0);
    let diffuse = diff * uniforms.lightColor;

    // === Specular ===
    let halfDir = normalize(mainLightDir + viewDir);
    let specPower = max(uniforms.specularPower, 1.0);  // Clamp to prevent issues
    let spec = pow(max(dot(worldNormal, halfDir), 0.0), specPower);
    let specular = spec * uniforms.specularColor.rgb;

    // === Combine Lighting ===
    let lightmapColor = lightmap.rgb;

    // Scene lighting: (ambient + diffuse) * lightmap
    let sceneLighting = (ambient + diffuse) * lightmapColor;

    // Apply to diffuse texture, then add specular and emissive
    let litDiffuse = baseDiffuse.rgb * in.color.rgb * sceneLighting;
    let finalColor = litDiffuse + specular; // + uniforms.emissiveColor.rgb;

    return vec4<f32>(finalColor, in.color.a);
}
