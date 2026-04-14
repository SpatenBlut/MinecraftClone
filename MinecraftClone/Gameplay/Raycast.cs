using System;
using Microsoft.Xna.Framework;
using MinecraftClone.World;

namespace MinecraftClone.Gameplay;

public class Raycast
{
    public static bool CastRay(World.World world, Vector3 origin, Vector3 direction, float maxDistance,
        out Vector3 hitBlock, out Vector3 adjacentBlock)
    {
        hitBlock = Vector3.Zero;
        adjacentBlock = Vector3.Zero;

        direction = Vector3.Normalize(direction);

        Vector3 currentPos = origin;
        Vector3 lastPos = origin;

        float step = 0.1f;
        float distance = 0f;

        while (distance < maxDistance)
        {
            int x = (int)Math.Floor(currentPos.X);
            int y = (int)Math.Floor(currentPos.Y);
            int z = (int)Math.Floor(currentPos.Z);

            if (world.IsBlockSolid(x, y, z))
            {
                hitBlock = new Vector3(x, y, z);
                adjacentBlock = new Vector3(
                    (int)Math.Floor(lastPos.X),
                    (int)Math.Floor(lastPos.Y),
                    (int)Math.Floor(lastPos.Z)
                );
                return true;
            }

            lastPos = currentPos;
            currentPos += direction * step;
            distance += step;
        }

        return false;
    }
}
