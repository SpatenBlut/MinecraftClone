using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.World;

namespace MinecraftClone.Rendering;

public class ChunkMesh
{
    private List<VertexPositionColorTexture> _vertices;
    private List<int> _indices;
    private VertexBuffer _vertexBuffer;
    private IndexBuffer _indexBuffer;
    private GraphicsDevice _graphicsDevice;

    public int TriangleCount => _indices.Count / 3;
    public bool IsEmpty => _vertices.Count == 0;

    public ChunkMesh(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _vertices = new List<VertexPositionColorTexture>();
        _indices = new List<int>();
    }

    public void Build(World.World world)
    {
        _vertices.Clear();
        _indices.Clear();

        for (int x = 0; x < world.Width; x++)
        {
            for (int y = 0; y < world.Height; y++)
            {
                for (int z = 0; z < world.Depth; z++)
                {
                    BlockType block = world.GetBlock(x, y, z);
                    if (block == BlockType.Air) continue;

                    AddBlockFaces(world, x, y, z, block);
                }
            }
        }

        UpdateBuffers();
    }

    private void AddBlockFaces(World.World world, int x, int y, int z, BlockType block)
    {
        // Nur sichtbare Faces rendern
        if (world.GetBlock(x, y + 1, z) == BlockType.Air)
            AddFace(x, y, z, FaceDirection.Top, block);

        if (world.GetBlock(x, y - 1, z) == BlockType.Air)
            AddFace(x, y, z, FaceDirection.Bottom, block);

        if (world.GetBlock(x + 1, y, z) == BlockType.Air)
            AddFace(x, y, z, FaceDirection.Right, block);

        if (world.GetBlock(x - 1, y, z) == BlockType.Air)
            AddFace(x, y, z, FaceDirection.Left, block);

        if (world.GetBlock(x, y, z + 1) == BlockType.Air)
            AddFace(x, y, z, FaceDirection.Front, block);

        if (world.GetBlock(x, y, z - 1) == BlockType.Air)
            AddFace(x, y, z, FaceDirection.Back, block);
    }

    // Minecraft-typische Flächenhelligkeit (baked face shading, kein dynamisches Licht)
    private static Color GetFaceColor(FaceDirection direction)
    {
        byte b = direction switch
        {
            FaceDirection.Top    => 255,           // 1.00 — volle Tageshelligkeit
            FaceDirection.Front  => 204,           // 0.80
            FaceDirection.Back   => 204,           // 0.80
            FaceDirection.Left   => 153,           // 0.60
            FaceDirection.Right  => 153,           // 0.60
            FaceDirection.Bottom => 127,           // 0.50 — dunkel wie Minecraft Unterseite
            _                    => 255
        };
        return new Color(b, b, b);
    }

    private void AddFace(int x, int y, int z, FaceDirection direction, BlockType block)
    {
        int vertexOffset = _vertices.Count;
        Vector2 texCoord = GetTextureCoordinates(block, direction);
        Color faceColor = GetFaceColor(direction);

        Vector3[] positions;

        switch (direction)
        {
            case FaceDirection.Top:
                positions = new[]
                {
                    new Vector3(x, y + 1, z),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x, y + 1, z + 1)
                };
                break;

            case FaceDirection.Bottom:
                positions = new[]
                {
                    new Vector3(x, y, z + 1),
                    new Vector3(x + 1, y, z + 1),
                    new Vector3(x + 1, y, z),
                    new Vector3(x, y, z)
                };
                break;

            case FaceDirection.Front:
                positions = new[]
                {
                    new Vector3(x, y, z + 1),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y, z + 1)
                };
                break;

            case FaceDirection.Back:
                positions = new[]
                {
                    new Vector3(x + 1, y, z),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x, y + 1, z),
                    new Vector3(x, y, z)
                };
                break;

            case FaceDirection.Left:
                positions = new[]
                {
                    new Vector3(x, y, z),
                    new Vector3(x, y + 1, z),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x, y, z + 1)
                };
                break;

            case FaceDirection.Right:
                positions = new[]
                {
                    new Vector3(x + 1, y, z + 1),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x + 1, y, z)
                };
                break;

            default:
                return;
        }

        float texSize = 0.25f; // 4x4 Texture Atlas

        _vertices.Add(new VertexPositionColorTexture(positions[0], faceColor, texCoord + new Vector2(0, texSize)));
        _vertices.Add(new VertexPositionColorTexture(positions[1], faceColor, texCoord + new Vector2(0, 0)));
        _vertices.Add(new VertexPositionColorTexture(positions[2], faceColor, texCoord + new Vector2(texSize, 0)));
        _vertices.Add(new VertexPositionColorTexture(positions[3], faceColor, texCoord + new Vector2(texSize, texSize)));

        _indices.Add(vertexOffset);
        _indices.Add(vertexOffset + 1);
        _indices.Add(vertexOffset + 2);
        _indices.Add(vertexOffset);
        _indices.Add(vertexOffset + 2);
        _indices.Add(vertexOffset + 3);
    }

    private Vector2 GetTextureCoordinates(BlockType block, FaceDirection direction)
    {
        float texSize = 0.25f;

        return block switch
        {
            BlockType.Grass => direction == FaceDirection.Top ? new Vector2(0, 0) :
                               direction == FaceDirection.Bottom ? new Vector2(texSize, 0) :
                               new Vector2(texSize * 2, 0),
            BlockType.Dirt => new Vector2(texSize, 0),
            BlockType.Stone => new Vector2(0, texSize),
            BlockType.Wood => new Vector2(texSize, texSize),
            BlockType.Leaves => new Vector2(texSize * 2, texSize),
            BlockType.Sand => new Vector2(texSize * 3, 0),
            _ => Vector2.Zero
        };
    }

    private void UpdateBuffers()
    {
        if (_vertices.Count == 0) return;

        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();

        _vertexBuffer = new VertexBuffer(_graphicsDevice, VertexPositionColorTexture.VertexDeclaration,
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
