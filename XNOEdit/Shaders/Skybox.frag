#version 330 core

in vec3 viewDir;
out vec4 FragColor;

uniform vec3 uSunDirection;

vec3 atmosphere(vec3 dir) {
    float sunDot = max(dot(dir, uSunDirection), 0.0);

    // Improved sky-ground blending
    float horizon = abs(dir.y);
    float horizonFalloff = pow(1.0 - horizon, 1.5);

    // Color palette
    vec3 zenithColor = vec3(0.25, 0.45, 0.75);     // Deep blue sky
    vec3 horizonSkyColor = vec3(0.7, 0.8, 0.95);   // Light blue-white at horizon
    vec3 horizonGroundColor = vec3(0.5, 0.55, 0.6); // Warm gray ground horizon
    vec3 nadirColor = vec3(0.3, 0.35, 0.4);        // Dark ground color

    vec3 color;

    if (dir.y > 0.0) {
        // Sky - blend from zenith to horizon
        float skyBlend = pow(dir.y, 0.7); // Slower falloff for more sky color
        color = mix(horizonSkyColor, zenithColor, skyBlend);

        // Sun glow in sky
        float sunGlow = pow(sunDot, 8.0) * 0.6;
        float sunCore = pow(sunDot, 256.0) * 1.5;
        vec3 sunColor = vec3(1.0, 0.95, 0.8);

        color += sunColor * sunGlow;
        color += vec3(1.0, 0.98, 0.9) * sunCore;
    } else {
        // Ground - blend from horizon down to nadir
        float groundBlend = pow(-dir.y, 0.5); // Faster falloff
        color = mix(horizonGroundColor, nadirColor, groundBlend);

        // Subtle ambient bounce light from 'sun' below horizon
        if (sunDot > 0.0) {
            float groundGlow = pow(sunDot, 4.0) * 0.15;
            color += vec3(0.4, 0.35, 0.3) * groundGlow;
        }
    }

    // Atmospheric fog near horizon for smooth transition
    float fogAmount = pow(horizonFalloff, 2.0) * 0.3;
    vec3 fogColor = vec3(0.75, 0.8, 0.9);
    color = mix(color, fogColor, fogAmount);

    return color;
}

void main()
{
    vec3 dir = normalize(viewDir);
    vec3 color = atmosphere(dir);

    FragColor = vec4(color, 1.0);
}
