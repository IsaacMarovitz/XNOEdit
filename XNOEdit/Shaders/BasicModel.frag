#version 330 core

out vec4 FragColor;

in vec3 FragPos;
in vec3 Normal;
in vec4 Color;

uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform vec3 uViewPos;

void main()
{
    // Desaturate light color for more subtle influence
    vec3 desaturatedLight = mix(vec3(1.0), uLightColor, 0.3);

    // Ambient
    float ambientStrength = 0.3;
    vec3 ambient = ambientStrength * desaturatedLight;

    // Diffuse
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(uLightDir);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * desaturatedLight;

    // Specular - keep mostly white for realistic highlights
    float specularStrength = 0.5;
    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32);
    vec3 specular = specularStrength * spec * vec3(1.0);

    // Combine lighting with vertex color
    vec3 result = (ambient + diffuse + specular) * Color.rgb;
    FragColor = vec4(result, Color.a);
}
