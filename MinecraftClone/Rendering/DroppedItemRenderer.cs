using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Gameplay;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

public class DroppedItemRenderer
{
    private readonly GraphicsDevice _gd;
    private readonly BasicEffect    _effect;
    private readonly Texture2D      _atlas;

    private readonly Dictionary<BlockType, VertexPositionColorTexture[]> _cache = new();

    private const float S     = 1f / 16f;
    private const float Scale = 0.22f;

    public DroppedItemRenderer(GraphicsDevice gd, Texture2D atlas)
    {
        _gd    = gd;
        _atlas  = atlas;
        _effect = new BasicEffect(gd)
        {
            TextureEnabled     = true,
            VertexColorEnabled = true,
            LightingEnabled    = false,
        };
    }

    public void Draw(List<DroppedItem> items, Matrix view, Matrix projection)
    {
        if (items.Count == 0) return;

        var prevRaster  = _gd.RasterizerState;
        var prevDepth   = _gd.DepthStencilState;
        var prevBlend   = _gd.BlendState;
        var prevSampler = _gd.SamplerStates[0];

        _gd.RasterizerState   = RasterizerState.CullCounterClockwise;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;
        _gd.SamplerStates[0]  = SamplerState.PointClamp;

        _effect.View       = view;
        _effect.Projection = projection;
        _effect.Texture    = _atlas;

        foreach (var item in items)
        {
            if (!_cache.TryGetValue(item.Block, out var verts))
            {
                verts = BuildCube(item.Block);
                _cache[item.Block] = verts;
            }

            // Center the cube on XZ, let it bob upward
            _effect.World =
                Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                Matrix.CreateScale(Scale) *
                Matrix.CreateRotationY(item.SpinAngle) *
                Matrix.CreateTranslation(item.Position + new Vector3(0, item.BobOffset, 0));

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _gd.SamplerStates[0] = SamplerState.PointClamp;
                _gd.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, 12);
            }
        }

        _gd.RasterizerState   = prevRaster;
        _gd.DepthStencilState = prevDepth;
        _gd.BlendState        = prevBlend;
        _gd.SamplerStates[0]  = prevSampler;
    }

    private static VertexPositionColorTexture[] BuildCube(BlockType block)
    {
        var verts = new VertexPositionColorTexture[36];
        int i = 0;

        void Face(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 uv, Color tint)
        {
            verts[i++] = new(v0, tint, uv + new Vector2(0, S));
            verts[i++] = new(v1, tint, uv + new Vector2(0, 0));
            verts[i++] = new(v2, tint, uv + new Vector2(S, 0));
            verts[i++] = new(v0, tint, uv + new Vector2(0, S));
            verts[i++] = new(v2, tint, uv + new Vector2(S, 0));
            verts[i++] = new(v3, tint, uv + new Vector2(S, S));
        }

        // Top
        Face(new(0,1,0), new(1,1,0), new(1,1,1), new(0,1,1), Tile(block, 0), Tint(block, 0));
        // Bottom
        Face(new(0,0,1), new(1,0,1), new(1,0,0), new(0,0,0), Tile(block, 1), Tint(block, 1));
        // Front
        Face(new(0,0,1), new(0,1,1), new(1,1,1), new(1,0,1), Tile(block, 2), Tint(block, 2));
        // Back
        Face(new(1,0,0), new(1,1,0), new(0,1,0), new(0,0,0), Tile(block, 3), Tint(block, 3));
        // Left
        Face(new(0,0,0), new(0,1,0), new(0,1,1), new(0,0,1), Tile(block, 4), Tint(block, 4));
        // Right
        Face(new(1,0,1), new(1,1,1), new(1,1,0), new(1,0,0), Tile(block, 5), Tint(block, 5));

        return verts;
    }

    // face index: 0=Top, 1=Bottom, 2..5=Sides
    private static Vector2 Tile(BlockType block, int face)
    {
        static Vector2 T(int col, int row) => new(col / 16f, row / 16f);
        bool isTop = face == 0, isBot = face == 1;
        return block switch
        {
            BlockType.Grass  => isTop ? T(0, 0) : isBot ? T(2, 0) : T(1, 0),
            BlockType.Dirt   => T(2, 0),
            BlockType.Stone  => T(3, 0),
            BlockType.Wood   => (isTop || isBot) ? T(5, 0) : T(4, 0),
            BlockType.Leaves => T(6, 0),
            BlockType.Sand   => T(7, 0),
            BlockType.Water  => T(8, 0),
            _                => Vector2.Zero
        };
    }

    private static Color Tint(BlockType block, int face)
    {
        if (block == BlockType.Grass && face == 0) return new Color(0x91, 0xBD, 0x59);
        if (block == BlockType.Leaves)
            return face == 0 ? new Color(0x91, 0xBD, 0x59) : new Color(0x77, 0xAB, 0x2F);
        return Color.White;
    }
}
