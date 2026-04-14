using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

public class BasicEffect3D
{
    private BasicEffect _effect;
    private GraphicsDevice _graphicsDevice;

    public Matrix World { get; set; }
    public Matrix View { get; set; }
    public Matrix Projection { get; set; }
    public Texture2D Texture { get; set; }

    // Fog (Minecraft-style Render-Distance-Fade)
    public bool FogEnabled  { get => _effect.FogEnabled;  set => _effect.FogEnabled  = value; }
    public Vector3 FogColor { get => _effect.FogColor;    set => _effect.FogColor    = value; }
    public float FogStart   { get => _effect.FogStart;    set => _effect.FogStart    = value; }
    public float FogEnd     { get => _effect.FogEnd;      set => _effect.FogEnd      = value; }

    public BasicEffect3D(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice);
        _effect.TextureEnabled    = true;
        _effect.VertexColorEnabled = true;  // Face-Helligkeit ist in Vertex-Farbe gebacken
        _effect.LightingEnabled   = false;  // Kein dynamisches Licht — wie Minecraft
    }

    public void Apply()
    {
        _effect.World      = World;
        _effect.View       = View;
        _effect.Projection = Projection;
        _effect.Texture    = Texture;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            // Pixel-Art: PointClamp NACH pass.Apply() setzen, da BasicEffect den State zurücksetzen kann
            _graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        }
    }
}
