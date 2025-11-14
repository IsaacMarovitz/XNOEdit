#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec4 aColor;

out vec3 FragPos;
out vec3 Normal;
out vec4 Color;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform float uVertColorStrength;

void main()
{
    vec4 worldPos = uModel * vec4(aPosition, 1.0);
    FragPos = worldPos.xyz;

    // Transform normal to world space (handles non-uniform scaling)
    Normal = mat3(transpose(inverse(uModel))) * aNormal;

    Color = mix(vec4(1.0), aColor, uVertColorStrength);

    gl_Position = uProjection * uView * worldPos;
}
