using System;
using Microsoft.Xna.Framework;

namespace MinecraftClone.Gameplay;

public class DroppedItem
{
    public Vector3   Position;
    public World.BlockType Block;
    public bool      Collected;

    private Vector3 _velocity;
    private float   _age;
    private bool    _grounded;

    private static readonly Random Rng = new();

    private const float HalfW       = 0.125f;
    private const float ItemH       = 0.25f;
    private const float Gravity     = -20f;
    private const float PickupDelay = 0.5f;
    private const float PickupRange = 1.0f;
    private const float MagnetRange = 2.5f;
    private const float DespawnTime = 300f;

    public float BobOffset => MathF.Abs(MathF.Sin(_age * 2f)) * 0.12f;
    public float SpinAngle  => _age * 1.8f;

    public DroppedItem(World.BlockType block, Vector3 spawnCenter)
    {
        Block    = block;
        Position = spawnCenter;
        _velocity = new Vector3(0f, 2f, 0f);
    }

    // Returns true when the item should be removed from the world
    public bool Update(float dt, World.World world, Vector3 playerFeet, Inventory inventory)
    {
        _age += dt;
        if (_age > DespawnTime) return true;

        // Magnetic pull + pickup
        if (_age > PickupDelay)
        {
            Vector3 playerCenter = playerFeet + new Vector3(0, 0.9f, 0);
            float distSq = Vector3.DistanceSquared(Position, playerCenter);

            if (distSq < MagnetRange * MagnetRange)
            {
                float dist = MathF.Sqrt(distSq);
                Vector3 dir = (playerCenter - Position) / dist;
                // Pull strength grows quadratically as item gets closer
                float t = 1f - dist / MagnetRange;
                float pull = t * t * 120f;
                _velocity += dir * pull * dt;
            }

            if (distSq < PickupRange * PickupRange)
            {
                if (inventory.AddToInventory(Block, 1))
                {
                    Collected = true;
                    return true;
                }
            }
        }

        // Gravity
        if (!_grounded)
            _velocity.Y += Gravity * dt;
        _velocity.Y = MathF.Max(_velocity.Y, -40f);

        // Ground friction
        if (_grounded)
        {
            float drag = MathF.Pow(0.05f, dt);
            _velocity.X *= drag;
            _velocity.Z *= drag;
        }

        // Axis-separated movement with block collision
        MoveAxis(ref Position, ref _velocity, _velocity.X * dt, 0, world, out _);
        MoveAxis(ref Position, ref _velocity, _velocity.Y * dt, 1, world, out bool hitY);
        _grounded = hitY && _velocity.Y >= -0.1f;
        MoveAxis(ref Position, ref _velocity, _velocity.Z * dt, 2, world, out _);

        return false;
    }

    private static void MoveAxis(ref Vector3 pos, ref Vector3 vel,
        float delta, int axis, World.World world, out bool hit)
    {
        hit = false;

        if (axis == 0) pos.X += delta;
        else if (axis == 1) pos.Y += delta;
        else pos.Z += delta;

        int minX = (int)Math.Floor(pos.X - HalfW);
        int minY = (int)Math.Floor(pos.Y);
        int minZ = (int)Math.Floor(pos.Z - HalfW);
        int maxX = (int)Math.Floor(pos.X + HalfW - 0.001f);
        int maxY = (int)Math.Floor(pos.Y + ItemH - 0.001f);
        int maxZ = (int)Math.Floor(pos.Z + HalfW - 0.001f);

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            if (!world.IsBlockSolid(x, y, z)) continue;

            hit = true;
            if (axis == 0) { pos.X -= delta; vel.X = 0; }
            else if (axis == 1) { pos.Y -= delta; vel.Y = 0; }
            else { pos.Z -= delta; vel.Z = 0; }
            return;
        }
    }
}
