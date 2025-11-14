#version 330 core

layout (location = 0) in vec3 aPosition;

out vec3 viewDir;

uniform mat4 uView;
uniform mat4 uProjection;

void main()
{
    // Calculate view direction by unprojecting the NDC position
    mat4 invProj = inverse(uProjection);
    mat4 invView = inverse(uView);

    vec4 clipPos = vec4(aPosition.xy, 1.0, 1.0);
    vec4 viewPos = invProj * clipPos;
    viewPos /= viewPos.w;

    vec4 worldPos = invView * viewPos;
    vec3 cameraPos = invView[3].xyz;

    viewDir = normalize(worldPos.xyz - cameraPos);

    gl_Position = vec4(aPosition, 1.0);
}
