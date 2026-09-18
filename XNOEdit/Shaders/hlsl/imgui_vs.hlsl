#include "imgui_common.hlsli"

void shaderMain(in float2 position : POSITION, in float2 uv : TEXCOORD0, in float4 color : COLOR, out Interpolators output)
{
    output.Position = mul(g_PushConstants.Mvp, float4(position, 0.0, 1.0));
    output.Color = color;
    output.UV = uv;
}
