using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Core;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

public class PlayerHeldItem
{
    private readonly GraphicsDevice _gd;
    private readonly BasicEffect    _effect;
    private readonly Texture2D      _atlas;

    private readonly VertexPositionColorTexture[] _verts = new VertexPositionColorTexture[36];
    private BlockType _cachedBlock = BlockType.Air;

    private const float ArmFov = 70f;
    private const float S = 1f / 16f;

    public PlayerHeldItem(GraphicsDevice gd, Texture2D atlas)
    {
        _gd    = gd;
        _atlas = atlas;
        _effect = new BasicEffect(gd)
        {
            TextureEnabled     = true,
            VertexColorEnabled = true,
            LightingEnabled    = false,
        };
    }

    public void Draw(Camera camera, BlockType block, float x, float y, float z, float scale)
    {
        if (block != _cachedBlock)
        {
            BuildGeometry(block);
            _cachedBlock = block;
        }

        float aspect = _gd.Viewport.AspectRatio;
        _effect.World      = BuildWorldMatrix(x, y, z, scale);
        _effect.View       = Matrix.Identity;
        _effect.Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(ArmFov), aspect, 0.05f, 10f);
        _effect.Texture    = _atlas;

        var prevRaster  = _gd.RasterizerState;
        var prevDepth   = _gd.DepthStencilState;
        var prevBlend   = _gd.BlendState;
        var prevSampler = _gd.SamplerStates[0];
        _gd.RasterizerState   = RasterizerState.CullCounterClockwise;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.BlendState        = BlendState.Opaque;
        _gd.SamplerStates[0]  = SamplerState.PointClamp;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.SamplerStates[0] = SamplerState.PointClamp;
            _gd.DrawUserPrimitives(PrimitiveType.TriangleList, _verts, 0, 12);
        }

        _gd.RasterizerState   = prevRaster;
        _gd.DepthStencilState = prevDepth;
        _gd.BlendState        = prevBlend;
        _gd.SamplerStates[0]  = prevSampler;
    }

    // Unit cube (0,0,0)→(1,1,1), vertex layout and winding matches ChunkMesh.AddFace.
    private void BuildGeometry(BlockType block)
    {
        int i = 0;

        void Face(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 uv, Color tint)
        {
            _verts[i++] = new(v0, tint, uv + new Vector2(0, S));
            _verts[i++] = new(v1, tint, uv + new Vector2(0, 0));
            _verts[i++] = new(v2, tint, uv + new Vector2(S, 0));
            _verts[i++] = new(v0, tint, uv + new Vector2(0, S));
            _verts[i++] = new(v2, tint, uv + new Vector2(S, 0));
            _verts[i++] = new(v3, tint, uv + new Vector2(S, S));
        }

        Face(new(0,1,0), new(1,1,0), new(1,1,1), new(0,1,1),
             Tile(block, FaceDirection.Top),    Tint(block, FaceDirection.Top));
        Face(new(0,0,1), new(1,0,1), new(1,0,0), new(0,0,0),
             Tile(block, FaceDirection.Bottom), Tint(block, FaceDirection.Bottom));
        Face(new(0,0,1), new(0,1,1), new(1,1,1), new(1,0,1),
             Tile(block, FaceDirection.Front),  Tint(block, FaceDirection.Front));
        Face(new(1,0,0), new(1,1,0), new(0,1,0), new(0,0,0),
             Tile(block, FaceDirection.Back),   Tint(block, FaceDirection.Back));
        Face(new(0,0,0), new(0,1,0), new(0,1,1), new(0,0,1),
             Tile(block, FaceDirection.Left),   Tint(block, FaceDirection.Left));
        Face(new(1,0,1), new(1,1,1), new(1,1,0), new(1,0,0),
             Tile(block, FaceDirection.Right),  Tint(block, FaceDirection.Right));
    }

    // Mirrors ChunkMesh.GetTextureCoordinates and BlockIconRenderer tile layout.
    private static Vector2 Tile(BlockType block, FaceDirection dir)
    {
        static Vector2 T(int col, int row) => new(col / 16f, row / 16f);
        return block switch
        {
            BlockType.Grass  => dir == FaceDirection.Top    ? T(0, 0)
                              : dir == FaceDirection.Bottom ? T(2, 0)
                              : T(1, 0),
            BlockType.Dirt   => T(2, 0),
            BlockType.Stone  => T(3, 0),
            BlockType.Wood   => (dir is FaceDirection.Top or FaceDirection.Bottom) ? T(5, 0) : T(4, 0),
            BlockType.Leaves => T(6, 0),
            BlockType.Sand   => T(7, 0),
            BlockType.Water  => T(8, 0),
            _                => Vector2.Zero
        };
    }

    // Mirrors BlockIconRenderer biome tints.
    // Grass sides/top get the standard grass tint.
    // Oak leaves use a lighter top tint and the standard leaf green on sides.
    private static Color Tint(BlockType block, FaceDirection dir)
    {
        if (block == BlockType.Grass && dir != FaceDirection.Bottom)
            return new Color(0x77, 0xAB, 0x2F);
        if (block == BlockType.Leaves)
            return dir == FaceDirection.Top
                ? new Color(0x91, 0xBD, 0x59)   // lighter top, matching BlockIconRenderer.TopTint
                : new Color(0x77, 0xAB, 0x2F);  // standard leaf green on sides
        return Color.White;
    }

    // MC 1.21 ItemInHandRenderer.renderItemInFirstPerson — 3D block item, right hand, no animation.
    // MC PoseStack order (→ reversed for MonoGame):
    //   step 1 (outermost): translate(x, y, z)          — camera-space placement
    //   step 2: rotateY(45°)                             — block/block.json firstperson_righthand rotation
    //   step 3 (innermost): scale(scale, scale, scale)   — block/block.json firstperson_righthand scale
    private static Matrix BuildWorldMatrix(float x, float y, float z, float scale) =>
          Matrix.CreateScale(scale)
        * Matrix.CreateRotationY(MathHelper.ToRadians(45f))
        * Matrix.CreateTranslation(x, y, z);
}
