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
// Minecraft Java verwendet keine physikalische Beleuchtung, sondern fixe
// Flächen-Multiplikatoren: Top=1.0, Nord/Süd=0.8, Ost/West=0.6, Unten=0.5
float DayBrightness;   // 0..1  (1.0 = voller Tag; für spätere Tag/Nacht-Implementierung)

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
    float4 Color    : COLOR0;   // RGB = Biom-Tint | A = Ambient Occlusion (0..1)
};

struct VSOut
{
    float4 Position  : SV_POSITION;
    float2 TexCoord  : TEXCOORD0;
    float3 Normal    : TEXCOORD1;
    float4 Color     : TEXCOORD2;   // als TEXCOORD2 damit AO bilinear interpoliert wird
    float  FogFactor : TEXCOORD3;
};

VSOut BlockVS(VSIn input)
{
    VSOut output;

    output.Position  = mul(input.Position, WorldViewProjection);
    output.TexCoord  = input.TexCoord;
    output.Normal    = mul(input.Normal, (float3x3)World);
    output.Color     = input.Color;

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

    // Minecraft Java 1.21 Flächen-Multiplikatoren (branchless für GLSL-Kompatibilität)
    float topMask    = step(0.5,  normal.y);                              // 1.0 wenn Oben
    float bottomMask = step(0.5, -normal.y);                              // 1.0 wenn Unten
    float sideMask   = 1.0 - topMask - bottomMask;
    float ewMask     = sideMask * step(0.5, abs(normal.x));               // Ost / West
    float nsMask     = sideMask * (1.0 - step(0.5, abs(normal.x)));       // Nord / Süd

    float faceMul = topMask    * 1.00
                  + bottomMask * 0.50
                  + ewMask     * 0.60
                  + nsMask     * 0.80;

    // Ambient Occlusion — smooth-interpoliert aus Vertex-Alpha
    float ao = input.Color.a;

    float3 lit = DayBrightness * faceMul * ao;

    // Textur × Biom-Tint × Beleuchtung
    float3 color = tex.rgb * input.Color.rgb * lit;

    // Nebel
    color = lerp(color, FogColor, input.FogFactor);

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
