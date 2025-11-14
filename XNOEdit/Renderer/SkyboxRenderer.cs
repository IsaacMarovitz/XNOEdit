using System.Numerics;
using Silk.NET.OpenGL;

namespace XNOEdit.Renderer
{
    public class SkyboxRenderer : IDisposable
    {
        private GL _gl;
        private Shader _shader;
        private uint _vao;
        private uint _vbo;

        public SkyboxRenderer(GL gl)
        {
            _gl = gl;
            CreateQuad();
            CreateShader();
        }

        private void CreateQuad()
        {
            // Full-screen quad in NDC coordinates
            var vertices = new float[]
            {
                -1.0f, -1.0f, 0.999f,  // Bottom-left (far depth)
                 1.0f, -1.0f, 0.999f,  // Bottom-right
                 1.0f,  1.0f, 0.999f,  // Top-right
                -1.0f,  1.0f, 0.999f   // Top-left
            };

            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (float* v = vertices)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
                }
            }

            unsafe
            {
                _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
                _gl.EnableVertexAttribArray(0);
            }

            _gl.BindVertexArray(0);
        }

        private void CreateShader()
        {
            const string vertexShader = @"
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
";

            const string fragmentShader = @"
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
";

            _shader = new Shader(_gl, vertexShader, fragmentShader);
        }

        public unsafe void Draw(Matrix4x4 view, Matrix4x4 projection, Vector3 sunDirection)
        {
            // Disable depth writing so skybox is always behind everything
            _gl.DepthMask(false);

            _shader.Use();
            _shader.SetUniform("uView", view);
            _shader.SetUniform("uProjection", projection);
            _shader.SetUniform("uSunDirection", sunDirection);

            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(GLEnum.TriangleFan, 0, 4);
            _gl.BindVertexArray(0);

            // Re-enable depth writing
            _gl.DepthMask(true);
        }

        public void Dispose()
        {
            _shader?.Dispose();
            if (_vao != 0)
                _gl.DeleteVertexArray(_vao);
            if (_vbo != 0)
                _gl.DeleteBuffer(_vbo);
        }
    }
}
