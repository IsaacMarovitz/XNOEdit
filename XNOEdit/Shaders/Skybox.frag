#version 330 core

in vec3 viewDir;
out vec4 FragColor;

uniform vec3 uSunDirection;
uniform vec3 uSunColor;

vec3 atmosphere(vec3 dir) {
    float sunDot = max(dot(dir, uSunDirection), 0.0);

    // Improved sky-ground blending
    float horizon = abs(dir.y);
    float horizonFalloff = pow(1.0 - horizon, 1.5);

    // Base color palette
    vec3 baseZenithColor = vec3(0.25, 0.45, 0.75);     // Deep blue sky
    vec3 baseHorizonSkyColor = vec3(0.7, 0.8, 0.95);   // Light blue-white at horizon
    vec3 horizonGroundColor = vec3(0.5, 0.55, 0.6);    // Warm gray ground horizon
    vec3 nadirColor = vec3(0.3, 0.35, 0.4);            // Dark ground color

    // Tint sky colors based on sun color
    float horizonTint = pow(1.0 - horizon, 2.0);  // Stronger at horizon
    vec3 zenithColor = mix(baseZenithColor, uSunColor * 0.6, horizonTint * 0.3);
    vec3 horizonSkyColor = mix(baseHorizonSkyColor, uSunColor, horizonTint * 0.7);

    vec3 color;

    if (dir.y > 0.0) {
        // Sky - blend from zenith to horizon
        float skyBlend = pow(dir.y, 0.7);
        color = mix(horizonSkyColor, zenithColor, skyBlend);

        // Sun glow in sky with custom color
        float sunGlow = pow(sunDot, 8.0) * 0.6;
        float sunCore = pow(sunDot, 256.0) * 1.5;

        // Apply sun color to glow and core
        color += uSunColor * sunGlow;
        color += uSunColor * sunCore;

        // Additional atmospheric scattering around sun
        float sunScatter = pow(sunDot, 3.0) * horizonTint * 0.4;
        color = mix(color, uSunColor, sunScatter);
    } else {
        // Ground - blend from horizon down to nadir
        float groundBlend = pow(-dir.y, 0.5);
        color = mix(horizonGroundColor, nadirColor, groundBlend);

        // Subtle ambient bounce light from 'sun' below horizon
        if (sunDot > 0.0) {
            float groundGlow = pow(sunDot, 4.0) * 0.15;
            color += uSunColor * 0.4 * groundGlow;
        }
    }

    // Atmospheric fog near horizon - tinted by sun
    float fogAmount = pow(horizonFalloff, 2.0) * 0.3;
    vec3 fogColor = mix(vec3(0.75, 0.8, 0.9), uSunColor, 0.5);
    color = mix(color, fogColor, fogAmount);

    return color;
}

void main()
{
    vec3 dir = normalize(viewDir);
    vec3 color = atmosphere(dir);

    FragColor = vec4(color, 1.0);
}
