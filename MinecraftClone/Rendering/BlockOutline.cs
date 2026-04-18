using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

public class BlockOutline
{
    private readonly GraphicsDevice            _gd;
    private readonly BasicEffect               _effect;
    private readonly List<VertexPositionColor> _verts = new(72);

    private const float HalfWidth = 0.003f; // Linienbreite in Welteinheiten

    public BlockOutline(GraphicsDevice gd)
    {
        _gd = gd;
        _effect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false,
        };
    }

    public void Draw(Vector3 block, Vector3 cameraPos, Matrix view, Matrix projection)
    {
        const float e = 0.0005f;
        float x0 = block.X - e, y0 = block.Y - e, z0 = block.Z - e;
        float x1 = block.X + 1 + e, y1 = block.Y + 1 + e, z1 = block.Z + 1 + e;

        var v000 = new Vector3(x0, y0, z0);
        var v100 = new Vector3(x1, y0, z0);
        var v110 = new Vector3(x1, y1, z0);
        var v010 = new Vector3(x0, y1, z0);
        var v001 = new Vector3(x0, y0, z1);
        var v101 = new Vector3(x1, y0, z1);
        var v111 = new Vector3(x1, y1, z1);
        var v011 = new Vector3(x0, y1, z1);

        _verts.Clear();
        var c = Color.Black;

        // 12 Kanten als kamera-ausgerichtete Quads
        AddEdge(v000, v100, c, cameraPos);
        AddEdge(v100, v110, c, cameraPos);
        AddEdge(v110, v010, c, cameraPos);
        AddEdge(v010, v000, c, cameraPos);

        AddEdge(v001, v101, c, cameraPos);
        AddEdge(v101, v111, c, cameraPos);
        AddEdge(v111, v011, c, cameraPos);
        AddEdge(v011, v001, c, cameraPos);

        AddEdge(v000, v001, c, cameraPos);
        AddEdge(v100, v101, c, cameraPos);
        AddEdge(v110, v111, c, cameraPos);
        AddEdge(v010, v011, c, cameraPos);

        _effect.World      = Matrix.Identity;
        _effect.View       = view;
        _effect.Projection = projection;

        var prevRaster = _gd.RasterizerState;
        _gd.RasterizerState = new RasterizerState
        {
            CullMode             = CullMode.None,
            DepthBias            = -0.00002f,
            SlopeScaleDepthBias  = -0.5f,
        };

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList,
                _verts.ToArray(), 0, _verts.Count / 3);
        }

        _gd.RasterizerState = prevRaster;
    }

    private void AddEdge(Vector3 a, Vector3 b, Color c, Vector3 camPos)
    {
        Vector3 dir    = Vector3.Normalize(b - a);
        Vector3 toMid  = Vector3.Normalize((a + b) * 0.5f - camPos);
        Vector3 perp   = Vector3.Normalize(Vector3.Cross(dir, toMid)) * HalfWidth;

        // Quad aus 2 Dreiecken
        _verts.Add(new(a - perp, c));
        _verts.Add(new(a + perp, c));
        _verts.Add(new(b + perp, c));

        _verts.Add(new(a - perp, c));
        _verts.Add(new(b + perp, c));
        _verts.Add(new(b - perp, c));
    }
}
