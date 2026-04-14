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
        if (world.GetBlock(x, y + 1, z) == BlockType.Air) AddFace(x, y, z, FaceDirection.Top,    block);
        if (world.GetBlock(x, y - 1, z) == BlockType.Air) AddFace(x, y, z, FaceDirection.Bottom, block);
        if (world.GetBlock(x + 1, y, z) == BlockType.Air) AddFace(x, y, z, FaceDirection.Right,  block);
        if (world.GetBlock(x - 1, y, z) == BlockType.Air) AddFace(x, y, z, FaceDirection.Left,   block);
        if (world.GetBlock(x, y, z + 1) == BlockType.Air) AddFace(x, y, z, FaceDirection.Front,  block);
        if (world.GetBlock(x, y, z - 1) == BlockType.Air) AddFace(x, y, z, FaceDirection.Back,   block);
    }

    // Biom-Tint: Gras-Oberseite und Blätter bekommen Grün, alles andere weiß (= unverändert)
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

    private void AddFace(int x, int y, int z, FaceDirection direction, BlockType block)
    {
        int     vertexOffset = _vertices.Count;
        Vector2 texCoord     = GetTextureCoordinates(block, direction);
        Color   tint         = GetBiomeTint(block, direction);
        Vector3 normal       = GetNormal(direction);

        float s = 1f / 16f; // ein Kachel-Schritt im 16×16-Atlas

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

        _vertices.Add(new BlockVertex(positions[0], texCoord + new Vector2(0, s), normal, tint));
        _vertices.Add(new BlockVertex(positions[1], texCoord + new Vector2(0, 0), normal, tint));
        _vertices.Add(new BlockVertex(positions[2], texCoord + new Vector2(s, 0), normal, tint));
        _vertices.Add(new BlockVertex(positions[3], texCoord + new Vector2(s, s), normal, tint));

        _indices.Add(vertexOffset);
        _indices.Add(vertexOffset + 1);
        _indices.Add(vertexOffset + 2);
        _indices.Add(vertexOffset);
        _indices.Add(vertexOffset + 2);
        _indices.Add(vertexOffset + 3);
    }

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
