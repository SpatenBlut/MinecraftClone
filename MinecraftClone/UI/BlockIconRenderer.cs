using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.UI;

/// <summary>
/// Rendert isometrische 3D-Block-Icons (wie Minecraft-Inventar) auf RenderTarget2D.
/// Drei sichtbare Flächen: Oben (hell), links (mittel), rechts (dunkel).
/// Wird einmal beim Start gerendert und dann als Textur verwendet.
/// </summary>
public sealed class BlockIconRenderer : IDisposable
{
    private readonly GraphicsDevice _gd;
    private readonly Texture2D      _atlas;
    private readonly BasicEffect    _effect;
    private readonly Dictionary<BlockType, RenderTarget2D> _cache = new();

    public const int IconSize = 128;

    public BlockIconRenderer(GraphicsDevice gd, Texture2D atlas)
    {
        _gd    = gd;
        _atlas = atlas;
        _effect = new BasicEffect(gd)
        {
            TextureEnabled     = true,
            VertexColorEnabled = true,  // Vertex-Farbe = Flächen-Helligkeit × Biom-Tint
            LightingEnabled    = false,
        };
    }

    /// <summary>Rendert alle Block-Icons vor. Einmal aus LoadContent() aufrufen.</summary>
    public void Initialize()
    {
        foreach (BlockType bt in Enum.GetValues<BlockType>())
        {
            if (bt == BlockType.Air) continue;
            _cache[bt] = BakeIcon(bt);
        }
    }

    public Texture2D GetIcon(BlockType block)
    {
        if (_cache.TryGetValue(block, out var cached)) return cached;
        // lazy fallback
        var icon = BakeIcon(block);
        _cache[block] = icon;
        return icon;
    }

    // ── Icon rendern ─────────────────────────────────────────────────────────

    private RenderTarget2D BakeIcon(BlockType block)
    {
        var rt = new RenderTarget2D(_gd, IconSize, IconSize, false,
                                    SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

        // Isometrische Kamera: Würfel (0,0,0)→(1,1,1), Kamera von oben-rechts-vorne
        var view       = Matrix.CreateLookAt(
            new Vector3(2f, 2f, 2f),
            new Vector3(0.5f, 0.5f, 0.5f),
            Vector3.Up);
        // Orthografische Projektion – Breite/Höhe so gewählt, dass Würfel gut reinpasst
        var projection = Matrix.CreateOrthographic(1.6f, 1.7f, 0.1f, 100f);

        _effect.World      = Matrix.Identity;
        _effect.View       = view;
        _effect.Projection = projection;
        _effect.Texture    = _atlas;

        // ── Geometrie aufbauen ───────────────────────────────────────────────
        var (verts, indices) = BuildCube(block);

        var vb = new VertexBuffer(_gd, VertexPositionColorTexture.VertexDeclaration,
                                  verts.Length, BufferUsage.WriteOnly);
        vb.SetData(verts);

        var ib = new IndexBuffer(_gd, IndexElementSize.SixteenBits,
                                 indices.Length, BufferUsage.WriteOnly);
        ib.SetData(indices);

        // ── Rendern ──────────────────────────────────────────────────────────
        _gd.SetRenderTarget(rt);
        _gd.Clear(Color.Transparent);

        _gd.BlendState        = BlendState.Opaque;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.RasterizerState   = RasterizerState.CullNone;
        _gd.SamplerStates[0]  = SamplerState.PointClamp;

        _gd.SetVertexBuffer(vb);
        _gd.Indices = ib;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            // pass.Apply() resets sampler states from the effect — override afterwards
            _gd.SamplerStates[0] = SamplerState.PointClamp;
            _gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indices.Length / 3);
        }

        _gd.SetRenderTarget(null);

        vb.Dispose();
        ib.Dispose();

        return rt;
    }

    // ── Cube-Geometrie ────────────────────────────────────────────────────────
    // Drei sichtbare Flächen: Oben (y=1), Rechts (x=1), Links/Vorne (z=1)
    // Helligkeit entspricht Minecraft Java: Top=1.0, N/S=0.8, O/W=0.6
    private (VertexPositionColorTexture[] verts, short[] indices) BuildCube(BlockType block)
    {
        const float uv = 1f / 16f;

        var verts = new List<VertexPositionColorTexture>();

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                     int col, int row, Color tint)
        {
            float u0 = col * uv, u1 = u0 + uv;
            float v0 = row * uv, v1 = v0 + uv;
            verts.Add(new VertexPositionColorTexture(a, tint, new Vector2(u0, v0)));
            verts.Add(new VertexPositionColorTexture(b, tint, new Vector2(u1, v0)));
            verts.Add(new VertexPositionColorTexture(c, tint, new Vector2(u1, v1)));
            verts.Add(new VertexPositionColorTexture(d, tint, new Vector2(u0, v1)));
        }

        // ── Oben (y=1) ───────────────────────────────────────────────── 1.00
        {
            var (col, row) = TopTile(block);
            AddQuad(
                new Vector3(0, 1, 0), new Vector3(1, 1, 0),
                new Vector3(1, 1, 1), new Vector3(0, 1, 1),
                col, row, TopTint(block));
        }

        // ── Rechts (x=1) ────────────────────────────────────────────── 0.60
        {
            var (col, row) = SideTile(block);
            var tint = Mul(SideTint(block), 0.60f);
            AddQuad(
                new Vector3(1, 1, 1), new Vector3(1, 1, 0),
                new Vector3(1, 0, 0), new Vector3(1, 0, 1),
                col, row, tint);
        }

        // ── Vorne/Links (z=1) ────────────────────────────────────────── 0.80
        {
            var (col, row) = SideTile(block);
            var tint = Mul(SideTint(block), 0.80f);
            AddQuad(
                new Vector3(0, 1, 1), new Vector3(1, 1, 1),
                new Vector3(1, 0, 1), new Vector3(0, 0, 1),
                col, row, tint);
        }

        // Indizes: jede Quad = 2 Dreiecke
        var idx = new List<short>();
        for (int f = 0; f < 3; f++)
        {
            short b = (short)(f * 4);
            idx.AddRange(new short[]
            {
                b, (short)(b+1), (short)(b+2),
                b, (short)(b+2), (short)(b+3)
            });
        }

        return (verts.ToArray(), idx.ToArray());
    }

    // ── Textur-Tile-Zuordnung (exakt wie im Chunk-Renderer) ──────────────────

    private static (int col, int row) TopTile(BlockType b) => b switch
    {
        BlockType.Grass => (0, 0),  // grass_block_top
        BlockType.Wood  => (5, 0),  // oak_log_top
        _               => MainTile(b)
    };

    private static (int col, int row) SideTile(BlockType b) => b switch
    {
        BlockType.Grass => (1, 0),  // grass_block_side
        BlockType.Wood  => (4, 0),  // oak_log (side)
        _               => MainTile(b)
    };

    private static (int col, int row) MainTile(BlockType b) => b switch
    {
        BlockType.Dirt   => (2, 0),
        BlockType.Stone  => (3, 0),
        BlockType.Leaves => (6, 0),
        BlockType.Sand   => (7, 0),
        BlockType.Water  => (8, 0),
        _                => (2, 0)
    };

    // ── Biom-Tints ────────────────────────────────────────────────────────────

    private static Color TopTint(BlockType b) => b switch
    {
        BlockType.Grass  => new Color(0x91, 0xBD, 0x59),
        BlockType.Leaves => new Color(0x77, 0xAB, 0x2F),
        _                => Color.White
    };

    // Seiten-Tint: Blätter grün auf allen Flächen, Gras-Seite ungefärbt
    private static Color SideTint(BlockType b) => b switch
    {
        BlockType.Leaves => new Color(0x77, 0xAB, 0x2F),
        _                => Color.White
    };

    // ── Hilfsfunktionen ───────────────────────────────────────────────────────

    private static Color Mul(Color c, float f) =>
        new Color((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f), c.A);

    public void Dispose()
    {
        _effect.Dispose();
        foreach (var (_, rt) in _cache)
            rt.Dispose();
        _cache.Clear();
    }
}
