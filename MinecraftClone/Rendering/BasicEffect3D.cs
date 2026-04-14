using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

/// <summary>
/// Wraps den custom Block-Shader (BasicShader.fx) und stellt Licht-/Nebel-Parameter bereit.
/// </summary>
public class BlockEffect
{
    private readonly Effect          _effect;
    private readonly EffectParameter _pWVP;
    private readonly EffectParameter _pWorld;
    private readonly EffectParameter _pTexture;
    private readonly EffectParameter _pCameraPos;
    private readonly EffectParameter _pSunDir;
    private readonly EffectParameter _pSunColor;
    private readonly EffectParameter _pAmbientSky;
    private readonly EffectParameter _pAmbientGround;
    private readonly EffectParameter _pFogColor;
    private readonly EffectParameter _pFogStart;
    private readonly EffectParameter _pFogEnd;

    public Matrix    World          { private get; set; }
    public Matrix    View           { private get; set; }
    public Matrix    Projection     { private get; set; }
    public Texture2D Texture        { private get; set; }
    public Vector3   CameraPosition { private get; set; }
    public Vector3   SunDirection   { private get; set; }
    public Vector3   SunColor       { private get; set; }
    public Vector3   AmbientSky     { private get; set; }
    public Vector3   AmbientGround  { private get; set; }
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
        _pSunDir        = effect.Parameters["SunDirection"];
        _pSunColor      = effect.Parameters["SunColor"];
        _pAmbientSky    = effect.Parameters["AmbientSky"];
        _pAmbientGround = effect.Parameters["AmbientGround"];
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
        _pSunDir.SetValue(SunDirection);
        _pSunColor.SetValue(SunColor);
        _pAmbientSky.SetValue(AmbientSky);
        _pAmbientGround.SetValue(AmbientGround);
        _pFogColor.SetValue(FogColor);
        _pFogStart.SetValue(FogStart);
        _pFogEnd.SetValue(FogEnd);

        _effect.CurrentTechnique.Passes[0].Apply();
    }
}
