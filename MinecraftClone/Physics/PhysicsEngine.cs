using System;
using Microsoft.Xna.Framework;
using MinecraftClone.World;

namespace MinecraftClone.Physics;

public class PhysicsEngine
{
    // Minecraft-genaue Physikwerte (konvertiert von Ticks auf Sekunden)
    // Minecraft: Schwerkraft 0.08 b/tick², Terminal 3.92 b/tick bei 20 ticks/s
    private const float Gravity = -28.0f;
    private const float TerminalVelocity = -78.4f;

    // Vertikaler Luftwiderstand: 0.98 pro Tick = 0.98^20 pro Sekunde
    // Erzeugt das charakteristische Minecraft-Sprung-Feeling (Apex wirkt "schwebend")
    private const float VerticalDragPerTick = 0.98f;

    public static Vector3 ApplyPhysics(Vector3 position, Vector3 velocity, AABB playerBox,
        World.World world, float deltaTime, bool isGrounded, out bool newGrounded, ref Vector3 newVelocity,
        float width = 0.6f, float height = 1.8f, float depth = 0.6f)
    {
        newVelocity = velocity;

        // Schwerkraft anwenden
        newVelocity.Y += Gravity * deltaTime;

        // Vertikaler Luftwiderstand (Minecraft: velocity.y *= 0.98 pro Tick)
        float verticalDrag = (float)Math.Pow(VerticalDragPerTick, 20.0 * deltaTime);
        newVelocity.Y *= verticalDrag;

        if (newVelocity.Y < TerminalVelocity)
            newVelocity.Y = TerminalVelocity;

        // Horizontaler Luftwiderstand (nur in der Luft)
        // Minecraft: velocity.x/z *= 0.91 pro Tick → 0.91^(20*dt) pro Sekunde
        if (!isGrounded)
        {
            float horizontalDrag = (float)Math.Pow(0.91, 20.0 * deltaTime);
            newVelocity.X *= horizontalDrag;
            newVelocity.Z *= horizontalDrag;
        }

        Vector3 movement = newVelocity * deltaTime;

        // Y-Achse (Vertikale Bewegung)
        position.Y += movement.Y;
        AABB yBox = AABB.FromPosition(position, width, height, depth);

        if (CheckCollisionWithAABB(yBox, world))
        {
            position.Y -= movement.Y;

            if (newVelocity.Y < 0)
            {
                // Am Boden
                position.Y = (float)Math.Floor(position.Y) + 0.001f;
                newGrounded = true;
            }
            else
            {
                // An Decke
                position.Y = (float)Math.Ceiling(position.Y + height) - height - 0.001f;
                newGrounded = false;
            }

            newVelocity.Y = 0;
        }
        else
        {
            newGrounded = false;
        }

        const float StepHeight = 0.6f;

        // X-Achse
        position.X += movement.X;
        AABB xBox = AABB.FromPosition(position, width, height, depth);

        if (CheckCollisionWithAABB(xBox, world))
        {
            bool stepped = false;
            if (isGrounded && movement.X != 0)
            {
                for (float step = 0.1f; step <= StepHeight; step += 0.1f)
                {
                    Vector3 testPos = new Vector3(position.X, position.Y + step, position.Z);
                    if (!CheckCollisionWithAABB(AABB.FromPosition(testPos, width, height, depth), world))
                    {
                        position.Y += step;
                        stepped = true;
                        break;
                    }
                }
            }
            if (!stepped)
            {
                position.X -= movement.X;
                newVelocity.X = 0;
            }
        }

        // Z-Achse
        position.Z += movement.Z;
        AABB zBox = AABB.FromPosition(position, width, height, depth);

        if (CheckCollisionWithAABB(zBox, world))
        {
            bool stepped = false;
            if (isGrounded && movement.Z != 0)
            {
                for (float step = 0.1f; step <= StepHeight; step += 0.1f)
                {
                    Vector3 testPos = new Vector3(position.X, position.Y + step, position.Z);
                    if (!CheckCollisionWithAABB(AABB.FromPosition(testPos, width, height, depth), world))
                    {
                        position.Y += step;
                        stepped = true;
                        break;
                    }
                }
            }
            if (!stepped)
            {
                position.Z -= movement.Z;
                newVelocity.Z = 0;
            }
        }

        return position;
    }

    private static bool CheckCollisionWithAABB(AABB playerBox, World.World world)
    {
        int minX = (int)Math.Floor(playerBox.Min.X);
        int minY = (int)Math.Floor(playerBox.Min.Y);
        int minZ = (int)Math.Floor(playerBox.Min.Z);
        int maxX = (int)Math.Floor(playerBox.Max.X);
        int maxY = (int)Math.Floor(playerBox.Max.Y);
        int maxZ = (int)Math.Floor(playerBox.Max.Z);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (world.IsBlockSolid(x, y, z))
                    {
                        AABB blockBox = new AABB(
                            new Vector3(x, y, z),
                            new Vector3(x + 1, y + 1, z + 1)
                        );

                        if (playerBox.Intersects(blockBox))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool CheckCollision(Vector3 position, AABB playerBox, World.World world,
        float width = 0.6f, float height = 1.8f, float depth = 0.6f)
    {
        AABB movedBox = AABB.FromPosition(position, width, height, depth);

        int minX = (int)Math.Floor(movedBox.Min.X);
        int minY = (int)Math.Floor(movedBox.Min.Y);
        int minZ = (int)Math.Floor(movedBox.Min.Z);
        int maxX = (int)Math.Floor(movedBox.Max.X);
        int maxY = (int)Math.Floor(movedBox.Max.Y);
        int maxZ = (int)Math.Floor(movedBox.Max.Z);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (world.IsBlockSolid(x, y, z))
                    {
                        AABB blockBox = new AABB(
                            new Vector3(x, y, z),
                            new Vector3(x + 1, y + 1, z + 1)
                        );

                        if (movedBox.Intersects(blockBox))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool IsGrounded(Vector3 position, AABB playerBox, World.World world,
        float width = 0.6f, float height = 1.8f, float depth = 0.6f)
    {
        Vector3 checkPosition = position - new Vector3(0, 0.1f, 0);
        AABB checkBox = AABB.FromPosition(checkPosition, width, height, depth);
        return CheckCollisionWithAABB(checkBox, world);
    }
}
