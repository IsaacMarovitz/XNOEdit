#version 330 core

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aColor;

out vec3 Color;
out float Distance;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCameraPos;

void main()
{
    vec4 worldPos = uModel * vec4(aPosition, 1.0);
    Color = aColor;
    Distance = length(worldPos.xyz - uCameraPos);
    gl_Position = uProjection * uView * worldPos;
}
