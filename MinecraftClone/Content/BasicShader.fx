#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#endif

// ── Matrizen ──────────────────────────────────────────────────────────────────
matrix WorldViewProjection;
matrix World;

// ── Textur (Point-Sampler = Pixel-Art-Look) ───────────────────────────────────
texture Texture;
sampler2D TexSampler = sampler_state
{
    Texture   = <Texture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
    AddressU  = Clamp;
    AddressV  = Clamp;
};

// ── Licht ─────────────────────────────────────────────────────────────────────
float3 SunDirection;    // normalisiert, Richtung VON Szene ZUR Sonne
float3 SunColor;        // warmes Gold
float3 AmbientSky;      // kühles Blau von oben
float3 AmbientGround;   // warmes Dunkel von unten

// ── Nebel ─────────────────────────────────────────────────────────────────────
float3 FogColor;
float  FogStart;
float  FogEnd;
float3 CameraPosition;

// ── Vertex-Shader ─────────────────────────────────────────────────────────────
struct VSIn
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal   : NORMAL0;
    float4 Color    : COLOR0;   // Biom-Tint, 0..1
};

struct VSOut
{
    float4 Position  : SV_POSITION;
    float2 TexCoord  : TEXCOORD0;
    float3 Normal    : TEXCOORD1;
    float4 Color     : TEXCOORD2;   // als TEXCOORD damit lineare Interpolation garantiert ist
    float  FogFactor : TEXCOORD3;
};

VSOut BlockVS(VSIn input)
{
    VSOut output;

    output.Position  = mul(input.Position, WorldViewProjection);
    output.TexCoord  = input.TexCoord;
    output.Normal    = mul(input.Normal, (float3x3)World);
    output.Color     = input.Color;

    // Nebelstärke aus Welt-Distanz
    float4 worldPos  = mul(input.Position, World);
    float  dist      = length(worldPos.xyz - CameraPosition);
    output.FogFactor = saturate((dist - FogStart) / (FogEnd - FogStart));

    return output;
}

// ── Pixel-Shader ──────────────────────────────────────────────────────────────
float4 BlockPS(VSOut input) : COLOR
{
    float4 tex    = tex2D(TexSampler, input.TexCoord);
    float3 normal = normalize(input.Normal);

    // Hemisphere-Ambient: Himmel (oben) → Erde (unten)
    float  hemi    = normal.y * 0.5 + 0.5;
    float3 ambient = lerp(AmbientGround, AmbientSky, hemi);

    // Gerichtetes Sonnenlicht (Lambert-Diffuse)
    float  NdotL   = max(dot(normal, SunDirection), 0.0);
    float3 lit     = ambient + SunColor * NdotL;

    // Textur × Biom-Tint × Beleuchtung
    float3 color   = tex.rgb * input.Color.rgb * lit;

    // Nebel zum Horizont hin überblenden
    color  = lerp(color, FogColor, input.FogFactor);

    return float4(color, tex.a);
}

// ── Technique ─────────────────────────────────────────────────────────────────
technique Block
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL BlockVS();
        PixelShader  = compile PS_SHADERMODEL BlockPS();
    }
}
