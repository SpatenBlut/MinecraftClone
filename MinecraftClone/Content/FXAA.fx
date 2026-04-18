#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#endif

float2 InverseViewportSize;

texture SceneTexture;
sampler2D SceneSampler = sampler_state
{
    Texture   = <SceneTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
    AddressU  = Clamp;
    AddressV  = Clamp;
};

struct VSIn
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOut
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

// Vertices are already in NDC space — pass straight through
VSOut FxaaVS(VSIn input)
{
    VSOut output;
    output.Position = float4(input.Position.xy, 0, 1);
    output.TexCoord = input.TexCoord;
    return output;
}

float Luma(float3 rgb) { return dot(rgb, float3(0.299, 0.587, 0.114)); }

// Simplified FXAA 3.11 (Timothy Lottes)
float4 FxaaPS(VSOut input) : COLOR
{
    float2 uv  = input.TexCoord;
    float2 rcp = InverseViewportSize;

    float3 rgbNW = tex2D(SceneSampler, uv + float2(-1, -1) * rcp).rgb;
    float3 rgbNE = tex2D(SceneSampler, uv + float2( 1, -1) * rcp).rgb;
    float3 rgbSW = tex2D(SceneSampler, uv + float2(-1,  1) * rcp).rgb;
    float3 rgbSE = tex2D(SceneSampler, uv + float2( 1,  1) * rcp).rgb;
    float3 rgbM  = tex2D(SceneSampler, uv).rgb;

    float lumaNW = Luma(rgbNW), lumaNE = Luma(rgbNE);
    float lumaSW = Luma(rgbSW), lumaSE = Luma(rgbSE);
    float lumaM  = Luma(rgbM);

    float lumaMin   = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax   = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    float lumaRange = lumaMax - lumaMin;

    if (lumaRange < max(0.0833, lumaMax * 0.166))
        return float4(rgbM, 1.0);

    float2 dir = float2(
        -((lumaNW + lumaNE) - (lumaSW + lumaSE)),
         ((lumaNW + lumaSW) - (lumaNE + lumaSE)));

    float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * 0.03125, 0.0078125);
    float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
    dir = clamp(dir * rcpDirMin, -8.0, 8.0) * rcp;

    float3 rgbA = 0.5 * (
        tex2D(SceneSampler, uv + dir * (1.0/3.0 - 0.5)).rgb +
        tex2D(SceneSampler, uv + dir * (2.0/3.0 - 0.5)).rgb);

    float3 rgbB = rgbA * 0.5 + 0.25 * (
        tex2D(SceneSampler, uv + dir * -0.5).rgb +
        tex2D(SceneSampler, uv + dir *  0.5).rgb);

    float lumaB = Luma(rgbB);
    if (lumaB < lumaMin || lumaB > lumaMax) return float4(rgbA, 1.0);
    return float4(rgbB, 1.0);
}

technique FXAA
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FxaaVS();
        PixelShader  = compile PS_SHADERMODEL FxaaPS();
    }
}
