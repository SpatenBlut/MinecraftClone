using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinecraftClone.Rendering;

public class BlockMiningBar
{
    private readonly GraphicsDevice        _gd;
    private readonly BasicEffect           _effect;
    private readonly VertexPositionColor[] _verts = new VertexPositionColor[12]; // 2 quads

    private const float BarWidth    = 0.76f;
    private const float BarHeight   = 0.055f;
    private const float OffsetBg    = 0.502f; // background sits just outside block face
    private const float OffsetFill  = 0.505f; // fill sits in front of background — no z-fight

    public BlockMiningBar(GraphicsDevice gd)
    {
        _gd = gd;
        _effect = new BasicEffect(gd)
        {
            VertexColorEnabled = true,
            TextureEnabled     = false,
            LightingEnabled    = false,
        };
    }

    public void Draw(Vector3 blockPos, Vector3 faceNormal, float progress,
                     Matrix view, Matrix projection)
    {
        if (progress <= 0f) return;

        // Snap to the nearest axis so diagonal hits at edges never produce a diagonal bar
        faceNormal = SnapToAxis(faceNormal);

        Vector3 blockCenter = blockPos + new Vector3(0.5f);

        Vector3 worldRef  = MathF.Abs(faceNormal.Y) > 0.9f ? Vector3.Backward : Vector3.Up;
        Vector3 faceRight = Vector3.Normalize(Vector3.Cross(worldRef, faceNormal));
        Vector3 faceUp    = Vector3.Normalize(Vector3.Cross(faceNormal, faceRight));

        // Background quad
        Vector3 bgCenter = blockCenter + faceNormal * OffsetBg;
        BuildQuad(_verts, 0, bgCenter, faceRight, faceUp,
                  BarWidth + 0.02f, BarHeight + 0.014f, new Color(0, 0, 0, 200));

        // Fill quad — further out so it always renders on top of the background
        float   fillW      = BarWidth * progress;
        Vector3 fillCenter = blockCenter + faceNormal * OffsetFill
                           + faceRight * ((fillW - BarWidth) * 0.5f);
        BuildQuad(_verts, 6, fillCenter, faceRight, faceUp,
                  fillW, BarHeight, ProgressColor(progress));

        _effect.World      = Matrix.Identity;
        _effect.View       = view;
        _effect.Projection = projection;

        var prevRaster = _gd.RasterizerState;
        var prevDepth  = _gd.DepthStencilState;
        var prevBlend  = _gd.BlendState;

        _gd.RasterizerState   = new RasterizerState { CullMode = CullMode.None, DepthBias = -0.00004f };
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.AlphaBlend;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, _verts, 0, 4);
        }

        _gd.RasterizerState   = prevRaster;
        _gd.DepthStencilState = prevDepth;
        _gd.BlendState        = prevBlend;
    }

    // Clamps any vector to the nearest cardinal axis unit vector (one of ±X, ±Y, ±Z).
    private static Vector3 SnapToAxis(Vector3 v)
    {
        float ax = MathF.Abs(v.X), ay = MathF.Abs(v.Y), az = MathF.Abs(v.Z);
        if (ax >= ay && ax >= az) return new Vector3(MathF.Sign(v.X), 0f, 0f);
        if (ay >= ax && ay >= az) return new Vector3(0f, MathF.Sign(v.Y), 0f);
        return new Vector3(0f, 0f, MathF.Sign(v.Z));
    }

    private static void BuildQuad(VertexPositionColor[] verts, int start,
        Vector3 center, Vector3 right, Vector3 up, float width, float height, Color color)
    {
        Vector3 r = right * (width  * 0.5f);
        Vector3 u = up    * (height * 0.5f);

        Vector3 tl = center - r + u;
        Vector3 tr = center + r + u;
        Vector3 bl = center - r - u;
        Vector3 br = center + r - u;

        verts[start + 0] = new(tl, color);
        verts[start + 1] = new(tr, color);
        verts[start + 2] = new(br, color);
        verts[start + 3] = new(tl, color);
        verts[start + 4] = new(br, color);
        verts[start + 5] = new(bl, color);
    }

    // Green → yellow → red as progress increases
    private static Color ProgressColor(float t)
    {
        if (t < 0.5f)
            return new Color((int)(t * 2f * 255), 220, 0);
        else
            return new Color(255, (int)((1f - (t - 0.5f) * 2f) * 220), 0);
    }
}
