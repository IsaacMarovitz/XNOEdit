struct PerFrameUniforms {
    model: mat4x4<f32>,
    view: mat4x4<f32>,
    projection: mat4x4<f32>,
    sunDirection: vec4<f32>,
    sunColor: vec4<f32>,
    cameraPosition: vec3<f32>,
    vertColorStrength: f32,
}

struct PerMeshUniforms {
    ambientColor: vec4<f32>,
    diffuseColor: vec4<f32>,
    specularColor: vec4<f32>,
    emissiveColor: vec4<f32>,
    specularPower: f32,
    alphaRef: f32,
    alpha: f32,
    blend: f32,
    specular: f32,
}

struct VertexInput {
    @location(0) position: vec3<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) color: vec4<f32>,
    @location(3) uv0: vec2<f32>,
    @location(4) uv1: vec2<f32>,
}

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) world_position: vec3<f32>,
    @location(1) normal: vec3<f32>,
    @location(2) tangent: vec3<f32>,
    @location(3) bitangent: vec3<f32>,
    @location(4) color: vec4<f32>,
    @location(5) uv0: vec2<f32>,
    @location(6) uv1: vec2<f32>
}

@group(0) @binding(0) var<uniform> per_frame: PerFrameUniforms;
@group(1) @binding(0) var<uniform> per_mesh: PerMeshUniforms;

@group(2) @binding(0) var texture_sampler: sampler;
@group(2) @binding(1) var main_texture: texture_2d<f32>;
@group(2) @binding(2) var blend_texture: texture_2d<f32>;
@group(2) @binding(3) var normal_texture: texture_2d<f32>;
@group(2) @binding(4) var lightmap_texture: texture_2d<f32>;

const WORLD_SPACE_NORMALS: i32 = 0;

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

    let worldPos = per_frame.model * vec4<f32>(in.position, 1.0);
    out.world_position = worldPos.xyz;

    // Transform normal to world space
    let normalMatrix = mat3x3<f32>(
        per_frame.model[0].xyz,
        per_frame.model[1].xyz,
        per_frame.model[2].xyz
    );

    out.normal = normalize(normalMatrix * in.normal);

    // Calculate tangent space for normal mapping
    let tbn = calculateTangentSpace(out.normal);
    out.tangent = tbn[0];
    out.bitangent = tbn[1];

    out.uv0 = in.uv0;
    out.uv1 = in.uv1;
    out.color = mix(vec4<f32>(1.0), in.color, per_frame.vertColorStrength);
    out.position = per_frame.projection * per_frame.view * worldPos;

    return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4<f32> {
    // Sample textures
    let mainColor = textureSample(main_texture, texture_sampler, in.uv0);
    let blendColor = textureSample(blend_texture, texture_sampler, in.uv0);
    let normalMap = textureSample(normal_texture, texture_sampler, in.uv0);
    let lightmap = textureSample(lightmap_texture, texture_sampler, in.uv1);

    // Blend main and blend texture using vertex alpha
    var textureColor: vec4<f32>;

    if (per_mesh.blend == 1.0) {
        textureColor = mix(blendColor, mainColor, in.color.a);
    } else {
        textureColor = mainColor;
    }

    // Apply material diffuse color to texture
    let baseDiffuse = textureColor * per_mesh.diffuseColor;

    if (baseDiffuse.a < per_mesh.alphaRef) {
        discard;
    }

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
    let mainLightDir = normalize(per_frame.sunDirection);
    let viewDir = normalize(per_frame.cameraPosition - in.world_position);

    // === Ambient ===
    let ambient = per_mesh.ambientColor.rgb * 0.3 * per_frame.sunColor.rgb;

    // === Diffuse ===
    let diff = max(dot(worldNormal, mainLightDir.rgb), 0.0);
    let diffuse = diff * per_frame.sunColor.rgb;

    // === Specular ===
    var specular = vec3(0.0, 0.0, 0.0);
    if (per_mesh.specular == 1.0) {
        let halfDir = normalize(mainLightDir.rgb + viewDir);
        let specPower = max(per_mesh.specularPower, 1.0);
        let spec = pow(max(dot(worldNormal, halfDir), 0.0), specPower);
        specular = spec * per_mesh.specularColor.rgb * textureColor.a;
    }

    // === Combine Lighting ===
    let lightmapColor = lightmap.rgb;
    let sceneLighting = (ambient + diffuse) * lightmapColor;

    let litDiffuse = baseDiffuse.rgb * in.color.rgb * sceneLighting;
    let finalColor = litDiffuse + specular; // + per_mesh.emissiveColor.rgb;

    return vec4<f32>(finalColor, 1.0);
}
