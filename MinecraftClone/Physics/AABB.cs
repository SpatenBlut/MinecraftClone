using Microsoft.Xna.Framework;

namespace MinecraftClone.Physics;

public struct AABB
{
    public Vector3 Min;
    public Vector3 Max;

    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public AABB(Vector3 position, float width, float height, float depth)
    {
        // Position ist die untere Mitte des Spielers
        Min = new Vector3(position.X - width / 2, position.Y, position.Z - depth / 2);
        Max = new Vector3(position.X + width / 2, position.Y + height, position.Z + depth / 2);
    }

    public static AABB FromPosition(Vector3 position, float width, float height, float depth)
    {
        return new AABB(
            new Vector3(position.X - width / 2, position.Y, position.Z - depth / 2),
            new Vector3(position.X + width / 2, position.Y + height, position.Z + depth / 2)
        );
    }

    public bool Intersects(AABB other)
    {
        return Max.X > other.Min.X && Min.X < other.Max.X &&
               Max.Y > other.Min.Y && Min.Y < other.Max.Y &&
               Max.Z > other.Min.Z && Min.Z < other.Max.Z;
    }

    public AABB Offset(Vector3 offset)
    {
        return new AABB(Min + offset, Max + offset);
    }
}
