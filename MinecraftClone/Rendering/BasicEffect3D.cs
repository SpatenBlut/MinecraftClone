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
        _effect.VertexColorEnabled = true;  // Face-Helligkeit ist in Vertex-Farbe gebacken
        _effect.LightingEnabled = false;    // Kein dynamisches Licht — wie Minecraft
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
