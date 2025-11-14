#version 330 core

in vec3 Color;
in float Distance;

out vec4 FragColor;

uniform float uFadeStart;
uniform float uFadeEnd;

void main()
{
    // Distance-based fading
    float alpha = 1.0 - smoothstep(uFadeStart, uFadeEnd, Distance);

    FragColor = vec4(Color, alpha);
}
