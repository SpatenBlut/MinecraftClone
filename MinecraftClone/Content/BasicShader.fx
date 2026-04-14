#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#endif

matrix World;
matrix View;
matrix Projection;

texture Texture;
sampler2D textureSampler = sampler_state
{
    Texture = <Texture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Wrap;
    AddressV = Wrap;
};

float3 LightDirection = float3(0.5, -1, 0.5);
float AmbientIntensity = 0.6;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : NORMAL0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : TEXCOORD1;
};

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

    output.TexCoord = input.TexCoord;
    output.Normal = mul(input.Normal, (float3x3)World);

    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 texColor = tex2D(textureSampler, input.TexCoord);

    float3 normal = normalize(input.Normal);
    float3 lightDir = normalize(LightDirection);

    float diffuse = max(dot(normal, -lightDir), 0.0);
    float lightIntensity = AmbientIntensity + (1.0 - AmbientIntensity) * diffuse;

    return texColor * lightIntensity;
}

technique BasicColorDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
