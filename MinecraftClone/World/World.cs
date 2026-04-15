using System;
using System.IO;

namespace MinecraftClone.World;

public class World
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Depth { get; private set; }

    private byte[,,] _blocks;

    public World(int width, int height, int depth)
    {
        Width = width;
        Height = height;
        Depth = depth;
        _blocks = new byte[width, height, depth];
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
            return BlockType.Air;

        return (BlockType)_blocks[x, y, z];
    }

    public void SetBlock(int x, int y, int z, BlockType blockType)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || z < 0 || z >= Depth)
            return;

        _blocks[x, y, z] = (byte)blockType;
    }

    public bool IsBlockSolid(int x, int y, int z)
    {
        var block = GetBlock(x, y, z);
        return block != BlockType.Air && block != BlockType.Water;
    }

    public void Generate()
    {
        // Schichten: y=0,1,2 → Stone | y=3,4 → Dirt | y=5 → Grass (flach)
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Depth; z++)
            {
                SetBlock(x, 0, z, BlockType.Stone);
                SetBlock(x, 1, z, BlockType.Stone);
                SetBlock(x, 2, z, BlockType.Stone);
                SetBlock(x, 3, z, BlockType.Dirt);
                SetBlock(x, 4, z, BlockType.Dirt);
                SetBlock(x, 5, z, BlockType.Grass);
            }
        }

        // Einige Bäume (Stamm beginnt bei y=6, direkt über dem Gras)
        Random random = new Random(42);
        for (int i = 0; i < 10; i++)
        {
            int x = random.Next(2, Width - 2);
            int z = random.Next(2, Depth - 2);
            int y = 6;

            // Stamm
            for (int h = 0; h < 5; h++)
                SetBlock(x, y + h, z, BlockType.Wood);

            // Blätter
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    for (int dy = 4; dy <= 6; dy++)
                    {
                        if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2 && dy == 4)
                            continue;
                        if (dx == 0 && dz == 0 && dy < 6)
                            continue;
                        SetBlock(x + dx, y + dy, z + dz, BlockType.Leaves);
                    }
                }
            }
        }
    }

    public void SaveToFile(string filename)
    {
        using (BinaryWriter writer = new BinaryWriter(File.Open(filename, FileMode.Create)))
        {
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(Depth);

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        writer.Write(_blocks[x, y, z]);
                    }
                }
            }
        }
    }

    public static World LoadFromFile(string filename)
    {
        using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
        {
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            int depth = reader.ReadInt32();

            World world = new World(width, height, depth);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int z = 0; z < depth; z++)
                    {
                        world._blocks[x, y, z] = reader.ReadByte();
                    }
                }
            }

            return world;
        }
    }
}
