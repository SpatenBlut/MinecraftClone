using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

public class ChunkMesh
{
    private List<BlockVertex> _vertices;
    private List<int>         _indices;
    private VertexBuffer      _vertexBuffer;
    private IndexBuffer       _indexBuffer;
    private GraphicsDevice    _graphicsDevice;

    public int  TriangleCount => _indices.Count / 3;
    public bool IsEmpty       => _vertices.Count == 0;

    public ChunkMesh(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _vertices       = new List<BlockVertex>();
        _indices        = new List<int>();
    }

    public void Build(World.World world)
    {
        _vertices.Clear();
        _indices.Clear();

        for (int x = 0; x < world.Width;  x++)
        for (int y = 0; y < world.Height; y++)
        for (int z = 0; z < world.Depth;  z++)
        {
            BlockType block = world.GetBlock(x, y, z);
            if (block == BlockType.Air) continue;
            AddBlockFaces(world, x, y, z, block);
        }

        UpdateBuffers();
    }

    private void AddBlockFaces(World.World world, int x, int y, int z, BlockType block)
    {
        if (world.GetBlock(x, y + 1, z) == BlockType.Air) AddFace(world, x, y, z, FaceDirection.Top,    block);
        if (world.GetBlock(x, y - 1, z) == BlockType.Air) AddFace(world, x, y, z, FaceDirection.Bottom, block);
        if (world.GetBlock(x + 1, y, z) == BlockType.Air) AddFace(world, x, y, z, FaceDirection.Right,  block);
        if (world.GetBlock(x - 1, y, z) == BlockType.Air) AddFace(world, x, y, z, FaceDirection.Left,   block);
        if (world.GetBlock(x, y, z + 1) == BlockType.Air) AddFace(world, x, y, z, FaceDirection.Front,  block);
        if (world.GetBlock(x, y, z - 1) == BlockType.Air) AddFace(world, x, y, z, FaceDirection.Back,   block);
    }

    // ── Biom-Tint ─────────────────────────────────────────────────────────────

    private static Color GetBiomeTint(BlockType block, FaceDirection direction)
    {
        if (block == BlockType.Grass && direction == FaceDirection.Top)
            return new Color(0x91, 0xBD, 0x59);   // Plains-Gras-Grün
        if (block == BlockType.Leaves)
            return new Color(0x77, 0xAB, 0x2F);   // Oak-Leaf-Grün
        return Color.White;
    }

    private static Vector3 GetNormal(FaceDirection direction) => direction switch
    {
        FaceDirection.Top    =>  Vector3.Up,
        FaceDirection.Bottom => -Vector3.Up,
        FaceDirection.Front  =>  Vector3.UnitZ,
        FaceDirection.Back   => -Vector3.UnitZ,
        FaceDirection.Right  =>  Vector3.UnitX,
        FaceDirection.Left   => -Vector3.UnitX,
        _                    =>  Vector3.Up
    };

    // ── Ambient Occlusion ─────────────────────────────────────────────────────

    // Minecraft-AO: 0 (dunkelst) .. 3 (hell) — zwei Seiten + Diagonale
    private static int VertexAO(bool side1, bool side2, bool corner)
    {
        if (side1 && side2) return 0;
        return 3 - ((side1 ? 1 : 0) + (side2 ? 1 : 0) + (corner ? 1 : 0));
    }

    // AO-Stufe 0..3 → Helligkeits-Multiplikator (identisch zu Minecraft Java)
    private static float AOToFloat(int ao) => ao switch
    {
        0 => 0.50f,
        1 => 0.65f,
        2 => 0.80f,
        _ => 1.00f
    };

    // Speichert Biom-Tint (RGB) und AO (Alpha) in einem Color-Wert
    private static Color TintWithAO(Color tint, float ao)
        => new Color(tint.R, tint.G, tint.B, (byte)(ao * 255f));

    /// <summary>
    /// Berechnet für jeden der 4 Eckpunkte einer Fläche den AO-Wert (0..1).
    /// Reihenfolge: [0..3] entspricht positions[0..3] in AddFace.
    /// </summary>
    private static float[] ComputeFaceAO(World.World world, int x, int y, int z, FaceDirection dir)
    {
        // Jede Zeile: s1x,s1y,s1z, s2x,s2y,s2z, cx,cy,cz — Offsets relativ zu (x,y,z)
        int[,] o;
        switch (dir)
        {
            case FaceDirection.Top:
                o = new int[,]
                {   // C0:(x,  y+1,z  )  C1:(x+1,y+1,z  )  C2:(x+1,y+1,z+1)  C3:(x,y+1,z+1)
                    { -1,+1, 0,   0,+1,-1,  -1,+1,-1 },
                    { +1,+1, 0,   0,+1,-1,  +1,+1,-1 },
                    { +1,+1, 0,   0,+1,+1,  +1,+1,+1 },
                    { -1,+1, 0,   0,+1,+1,  -1,+1,+1 },
                };
                break;
            case FaceDirection.Bottom:
                o = new int[,]
                {
                    { -1,-1, 0,   0,-1,+1,  -1,-1,+1 },
                    { +1,-1, 0,   0,-1,+1,  +1,-1,+1 },
                    { +1,-1, 0,   0,-1,-1,  +1,-1,-1 },
                    { -1,-1, 0,   0,-1,-1,  -1,-1,-1 },
                };
                break;
            case FaceDirection.Front: // +Z
                o = new int[,]
                {
                    { -1, 0,+1,   0,-1,+1,  -1,-1,+1 },
                    { -1, 0,+1,   0,+1,+1,  -1,+1,+1 },
                    { +1, 0,+1,   0,+1,+1,  +1,+1,+1 },
                    { +1, 0,+1,   0,-1,+1,  +1,-1,+1 },
                };
                break;
            case FaceDirection.Back: // -Z
                o = new int[,]
                {
                    { +1, 0,-1,   0,-1,-1,  +1,-1,-1 },
                    { +1, 0,-1,   0,+1,-1,  +1,+1,-1 },
                    { -1, 0,-1,   0,+1,-1,  -1,+1,-1 },
                    { -1, 0,-1,   0,-1,-1,  -1,-1,-1 },
                };
                break;
            case FaceDirection.Left: // -X
                o = new int[,]
                {
                    { -1, 0,-1,  -1,-1, 0,  -1,-1,-1 },
                    { -1, 0,-1,  -1,+1, 0,  -1,+1,-1 },
                    { -1, 0,+1,  -1,+1, 0,  -1,+1,+1 },
                    { -1, 0,+1,  -1,-1, 0,  -1,-1,+1 },
                };
                break;
            case FaceDirection.Right: // +X
                o = new int[,]
                {
                    { +1, 0,+1,  +1,-1, 0,  +1,-1,+1 },
                    { +1, 0,+1,  +1,+1, 0,  +1,+1,+1 },
                    { +1, 0,-1,  +1,+1, 0,  +1,+1,-1 },
                    { +1, 0,-1,  +1,-1, 0,  +1,-1,-1 },
                };
                break;
            default:
                return new float[] { 1f, 1f, 1f, 1f };
        }

        var result = new float[4];
        for (int i = 0; i < 4; i++)
        {
            bool s1 = world.IsBlockSolid(x + o[i, 0], y + o[i, 1], z + o[i, 2]);
            bool s2 = world.IsBlockSolid(x + o[i, 3], y + o[i, 4], z + o[i, 5]);
            bool c  = world.IsBlockSolid(x + o[i, 6], y + o[i, 7], z + o[i, 8]);
            result[i] = AOToFloat(VertexAO(s1, s2, c));
        }
        return result;
    }

    // ── Fläche aufbauen ───────────────────────────────────────────────────────

    private void AddFace(World.World world, int x, int y, int z, FaceDirection direction, BlockType block)
    {
        int     vertexOffset = _vertices.Count;
        Vector2 texCoord     = GetTextureCoordinates(block, direction);
        Color   tintBase     = GetBiomeTint(block, direction);
        Vector3 normal       = GetNormal(direction);

        float s = 1f / 16f;

        Vector3[] positions;
        switch (direction)
        {
            case FaceDirection.Top:
                positions = new[]
                {
                    new Vector3(x,     y + 1, z),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x,     y + 1, z + 1)
                };
                break;

            case FaceDirection.Bottom:
                positions = new[]
                {
                    new Vector3(x,     y, z + 1),
                    new Vector3(x + 1, y, z + 1),
                    new Vector3(x + 1, y, z),
                    new Vector3(x,     y, z)
                };
                break;

            case FaceDirection.Front:
                positions = new[]
                {
                    new Vector3(x,     y,     z + 1),
                    new Vector3(x,     y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y,     z + 1)
                };
                break;

            case FaceDirection.Back:
                positions = new[]
                {
                    new Vector3(x + 1, y,     z),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x,     y + 1, z),
                    new Vector3(x,     y,     z)
                };
                break;

            case FaceDirection.Left:
                positions = new[]
                {
                    new Vector3(x, y,     z),
                    new Vector3(x, y + 1, z),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x, y,     z + 1)
                };
                break;

            case FaceDirection.Right:
                positions = new[]
                {
                    new Vector3(x + 1, y,     z + 1),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x + 1, y,     z)
                };
                break;

            default:
                return;
        }

        // AO pro Eckpunkt berechnen (standard Minecraft smooth-lighting)
        float[] ao = ComputeFaceAO(world, x, y, z, direction);

        _vertices.Add(new BlockVertex(positions[0], texCoord + new Vector2(0, s), normal, TintWithAO(tintBase, ao[0])));
        _vertices.Add(new BlockVertex(positions[1], texCoord + new Vector2(0, 0), normal, TintWithAO(tintBase, ao[1])));
        _vertices.Add(new BlockVertex(positions[2], texCoord + new Vector2(s, 0), normal, TintWithAO(tintBase, ao[2])));
        _vertices.Add(new BlockVertex(positions[3], texCoord + new Vector2(s, s), normal, TintWithAO(tintBase, ao[3])));

        // Diagonale auf die hellere Seite legen, um den "dunklen Streifen"-Artefakt zu vermeiden
        // (identisch zu Minecraft Javas smooth-lighting Triangulierung)
        if (ao[0] + ao[2] >= ao[1] + ao[3])
        {
            // Standard-Diagonale 0↔2
            _indices.Add(vertexOffset);
            _indices.Add(vertexOffset + 1);
            _indices.Add(vertexOffset + 2);
            _indices.Add(vertexOffset);
            _indices.Add(vertexOffset + 2);
            _indices.Add(vertexOffset + 3);
        }
        else
        {
            // Gedrehte Diagonale 1↔3
            _indices.Add(vertexOffset);
            _indices.Add(vertexOffset + 1);
            _indices.Add(vertexOffset + 3);
            _indices.Add(vertexOffset + 1);
            _indices.Add(vertexOffset + 2);
            _indices.Add(vertexOffset + 3);
        }
    }

    // ── Atlas-Koordinaten ─────────────────────────────────────────────────────

    private static Vector2 Tile(int col, int row) => new Vector2(col / 16f, row / 16f);

    private static Vector2 GetTextureCoordinates(BlockType block, FaceDirection direction)
    {
        return block switch
        {
            BlockType.Grass  => direction == FaceDirection.Top    ? Tile(0, 0)
                              : direction == FaceDirection.Bottom ? Tile(2, 0)
                              : Tile(1, 0),
            BlockType.Dirt   => Tile(2, 0),
            BlockType.Stone  => Tile(3, 0),
            BlockType.Wood   => (direction == FaceDirection.Top || direction == FaceDirection.Bottom)
                              ? Tile(5, 0) : Tile(4, 0),
            BlockType.Leaves => Tile(6, 0),
            BlockType.Sand   => Tile(7, 0),
            BlockType.Water  => Tile(8, 0),
            _                => Vector2.Zero
        };
    }

    // ── Buffer ────────────────────────────────────────────────────────────────

    private void UpdateBuffers()
    {
        if (_vertices.Count == 0) return;

        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();

        _vertexBuffer = new VertexBuffer(_graphicsDevice, BlockVertex.VertexDeclaration,
            _vertices.Count, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(_vertices.ToArray());

        _indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.ThirtyTwoBits,
            _indices.Count, BufferUsage.WriteOnly);
        _indexBuffer.SetData(_indices.ToArray());
    }

    public void Draw()
    {
        if (IsEmpty) return;

        _graphicsDevice.SetVertexBuffer(_vertexBuffer);
        _graphicsDevice.Indices = _indexBuffer;
        _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, TriangleCount);
    }

    public void Dispose()
    {
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
    }
}

public enum FaceDirection
{
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right
}
