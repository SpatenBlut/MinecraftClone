using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>
/// Wraps den custom Block-Shader (BasicShader.fx).
/// Beleuchtung: Minecraft-Java-Flächen-Multiplikatoren + Ambient Occlusion aus Vertex-Alpha.
/// </summary>
public class BlockEffect
{
    private readonly Effect          _effect;
    private readonly EffectParameter _pWVP;
    private readonly EffectParameter _pWorld;
    private readonly EffectParameter _pTexture;
    private readonly EffectParameter _pCameraPos;
    private readonly EffectParameter _pDayBrightness;
    private readonly EffectParameter _pFogColor;
    private readonly EffectParameter _pFogStart;
    private readonly EffectParameter _pFogEnd;

    public Matrix    World          { private get; set; }
    public Matrix    View           { private get; set; }
    public Matrix    Projection     { private get; set; }
    public Texture2D Texture        { private get; set; }
    public Vector3   CameraPosition { private get; set; }
    public float     DayBrightness  { private get; set; } = 1.0f;
    public Vector3   FogColor       { private get; set; }
    public float     FogStart       { private get; set; }
    public float     FogEnd         { private get; set; }

    public BlockEffect(Effect effect)
    {
        _effect         = effect;
        _pWVP           = effect.Parameters["WorldViewProjection"];
        _pWorld         = effect.Parameters["World"];
        _pTexture       = effect.Parameters["Texture"];
        _pCameraPos     = effect.Parameters["CameraPosition"];
        _pDayBrightness = effect.Parameters["DayBrightness"];
        _pFogColor      = effect.Parameters["FogColor"];
        _pFogStart      = effect.Parameters["FogStart"];
        _pFogEnd        = effect.Parameters["FogEnd"];
    }

    public void Apply()
    {
        _pWVP.SetValue(World * View * Projection);
        _pWorld.SetValue(World);
        _pTexture.SetValue(Texture);
        _pCameraPos.SetValue(CameraPosition);
        _pDayBrightness.SetValue(DayBrightness);
        _pFogColor.SetValue(FogColor);
        _pFogStart.SetValue(FogStart);
        _pFogEnd.SetValue(FogEnd);

        _effect.CurrentTechnique.Passes[0].Apply();
    }
}
