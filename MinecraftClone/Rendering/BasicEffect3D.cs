using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

public class BasicEffect3D
{
    private BasicEffect _effect;

    public Matrix World { get; set; }
    public Matrix View { get; set; }
    public Matrix Projection { get; set; }
    public Texture2D Texture { get; set; }

    public BasicEffect3D(GraphicsDevice graphicsDevice)
    {
        _effect = new BasicEffect(graphicsDevice);
        _effect.TextureEnabled = true;
        _effect.LightingEnabled = true;
        _effect.EnableDefaultLighting();
        _effect.PreferPerPixelLighting = false;

        // Ambient light
        _effect.AmbientLightColor = new Vector3(0.6f, 0.6f, 0.6f);

        // Directional light
        _effect.DirectionalLight0.Enabled = true;
        _effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.5f, -1f, 0.5f));
        _effect.DirectionalLight0.DiffuseColor = new Vector3(0.4f, 0.4f, 0.4f);
    }

    public void Apply()
    {
        _effect.World = World;
        _effect.View = View;
        _effect.Projection = Projection;
        _effect.Texture = Texture;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
        }
    }
}
