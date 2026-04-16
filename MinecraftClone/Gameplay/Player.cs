using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Core;
using MinecraftClone.Physics;
using MinecraftClone.World;

namespace MinecraftClone.Gameplay;

public class Player
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Camera Camera { get; private set; }
    public AABB CollisionBox { get; private set; }

    public float Health { get; set; } = 20f;
    public const float MaxHealth = 20f;
    public bool IsAlive => Health > 0;
    public Vector3 SpawnPoint = new Vector3(32f, 10f, 32f);

    public bool IsSprinting => _isSprinting;
    public bool IsSneaking => _isSneaking;

    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isSneaking;
    private float _jumpCooldown = 0f;
    private float _fallDistance = 0f;

    // Minecraft-genaue Bewegungswerte (Blöcke/Sekunde)
    private const float WalkSpeed    = 4.317f;   // 0.21585 b/tick * 20
    private const float SprintSpeed  = 5.612f;   // 0.2806  b/tick * 20
    private const float SneakSpeed   = 1.295f;   // 0.065   b/tick * 20
    private const float JumpStrength = 9.5f;     // angepasst für ~1.25 Blöcke Sprunghöhe (Minecraft-Standard)
    private const float JumpCooldownTime = 0.1f;

    // Spieler-Dimensionen
    private const float PlayerWidth    = 0.6f;
    private const float PlayerDepth    = 0.6f;
    private const float NormalHeight   = 1.8f;
    private const float SneakHeight    = 1.5f;
    private const float NormalEyeHeight = 1.62f;
    private const float SneakEyeHeight  = 1.27f;

    private MouseState _lastMouseState;
    private KeyboardState _lastKeyState;
    private KeyboardState _lastFrameKeyState;

    // Gebufferte Inputs — werden jeden Frame gesetzt, beim nächsten Tick konsumiert
    private bool  _jumpBuffered    = false;
    private float _jumpBufferTimer = 0f;
    private const float JumpBufferWindow = 0.15f; // 3 Ticks Puffer-Fenster

    // Für Render-Interpolation zwischen Ticks
    public Vector3 PreviousPosition;
    public Vector3 RenderPosition;
    private float _currentEyeHeight = NormalEyeHeight;

    // Smooth Sneak-Übergang (nicht instant wie in Minecraft)
    private float _smoothEyeHeight = NormalEyeHeight;
    private const float EyeHeightSpeed = 4f; // Einheiten/Sekunde

    public Player(Vector3 startPosition, float aspectRatio)
    {
        Position = startPosition;
        PreviousPosition = startPosition;
        RenderPosition = startPosition;
        Camera = new Camera(startPosition + new Vector3(0, NormalEyeHeight, 0), aspectRatio);
        CollisionBox = new AABB(Vector3.Zero, PlayerWidth, NormalHeight, PlayerDepth);
        Velocity = Vector3.Zero;
    }

    // Wird exakt 20x pro Sekunde aufgerufen (fixer 50ms Tick wie Minecraft)
    // inputEnabled=false: Physik (Schwerkraft, Kollision) läuft weiter, aber keine Tastatur-/Maus-Eingabe
    public void Tick(float deltaTime, World.World world, bool inputEnabled = true)
    {
        if (_jumpCooldown > 0)
            _jumpCooldown -= deltaTime;

        if (_jumpBufferTimer > 0)
        {
            _jumpBufferTimer -= deltaTime;
            if (_jumpBufferTimer <= 0)
                _jumpBuffered = false;
        }

        if (inputEnabled)
            HandleInput(deltaTime, world);
        else if (_isGrounded)
        {
            Velocity.X = 0f;
            Velocity.Z = 0f;
        }

        // Schleich-Kantenschutz
        if (_isSneaking && _isGrounded && (Velocity.X != 0f || Velocity.Z != 0f))
        {
            Vector3 testPos = Position + new Vector3(Velocity.X * deltaTime, 0f, Velocity.Z * deltaTime);
            if (!PhysicsEngine.IsGrounded(testPos, CollisionBox, world, PlayerWidth, NormalHeight, PlayerDepth))
            {
                Velocity.X = 0f;
                Velocity.Z = 0f;
            }
        }

        float currentHeight = _isSneaking ? SneakHeight : NormalHeight;

        bool wasGrounded = _isGrounded;
        float posYBefore = Position.Y;

        Vector3 newVelocity = Velocity;
        Position = PhysicsEngine.ApplyPhysics(Position, newVelocity, CollisionBox, world, deltaTime,
            _isGrounded, out _isGrounded, ref newVelocity, PlayerWidth, currentHeight, PlayerDepth);
        Velocity = newVelocity;

        // ── Falldamage (Minecraft-Java-Formel: Schaden = ceil(Fallhöhe - 3)) ──────
        float dy = posYBefore - Position.Y;   // positiv beim Fallen
        if (!wasGrounded && _isGrounded)
        {
            // Gerade gelandet
            if (dy > 0f) _fallDistance += dy;
            int bx = (int)MathF.Floor(Position.X);
            int by = (int)MathF.Floor(Position.Y);
            int bz = (int)MathF.Floor(Position.Z);
            bool inWater = world.GetBlock(bx, by, bz) == BlockType.Water
                        || world.GetBlock(bx, by + 1, bz) == BlockType.Water;
            if (!inWater && _fallDistance > 3f)
            {
                TakeDamage(MathF.Ceiling(_fallDistance - 3f));
                if (!IsAlive) Respawn();
            }
            _fallDistance = 0f;
        }
        else if (!_isGrounded && dy > 0f)
        {
            _fallDistance += dy;              // akkumuliert während des Falls
        }
        else if (wasGrounded && !_isGrounded)
        {
            _fallDistance = 0f;              // Boden gerade verlassen (Sprung / Kante)
        }
        // ─────────────────────────────────────────────────────────────────────────

        if (_isSprinting && Velocity.X == 0f && Velocity.Z == 0f && _isGrounded)
            _isSprinting = false;

        CollisionBox = new AABB(Vector3.Zero, PlayerWidth, currentHeight, PlayerDepth);
        _currentEyeHeight = _isSneaking ? SneakEyeHeight : NormalEyeHeight;
    }

    // Speichert den Zustand vor dem Tick für Interpolation
    public void SaveTickState()
    {
        PreviousPosition = Position;
    }

    // Setzt die interpolierte Render-Position (alpha = 0..1 zwischen letztem und aktuellem Tick)
    public void SetRenderPosition(float alpha, float deltaTime)
    {
        RenderPosition = Vector3.Lerp(PreviousPosition, Position, alpha);

        // Augenhöhe smooth Richtung Ziel bewegen (Sneak-Übergang wie Minecraft)
        float step = EyeHeightSpeed * deltaTime;
        if (_smoothEyeHeight < _currentEyeHeight)
            _smoothEyeHeight = Math.Min(_smoothEyeHeight + step, _currentEyeHeight);
        else if (_smoothEyeHeight > _currentEyeHeight)
            _smoothEyeHeight = Math.Max(_smoothEyeHeight - step, _currentEyeHeight);

        Camera.IsSprinting = _isSprinting;
        Camera.Position = RenderPosition + new Vector3(0, _smoothEyeHeight, 0);
    }

    // Jeden Frame aufrufen — buffert kurze Tastendrücke die zwischen Ticks liegen könnten
    public void PollInput()
    {
        var keyState = Keyboard.GetState();
        if (keyState.IsKeyDown(Keys.Space) && _lastFrameKeyState.IsKeyUp(Keys.Space))
        {
            _jumpBuffered    = true;
            _jumpBufferTimer = JumpBufferWindow;
        }
        _lastFrameKeyState = keyState;
    }

    public void UpdateCamera(GameTime gameTime, GraphicsDevice graphicsDevice, bool captureMouseInput = true)
    {
        Camera.Update(gameTime, graphicsDevice, captureMouseInput);
    }

    private void HandleInput(float deltaTime, World.World world)
    {
        var keyState = Keyboard.GetState();
        var mouseState = Mouse.GetState();

        // --- Zustand: Schleichen und Sprinten ---
        bool wantSneak = keyState.IsKeyDown(Keys.LeftShift);

        // Sprint starten: LeftCtrl + W (nicht beim Schleichen)
        if (!_isSprinting && keyState.IsKeyDown(Keys.LeftControl)
            && keyState.IsKeyDown(Keys.W) && !wantSneak)
        {
            _isSprinting = true;
        }

        // Sprint abbrechen: W losgelassen, Schleichen, oder rückwärts/seitwärts
        if (_isSprinting && (!keyState.IsKeyDown(Keys.W) || wantSneak))
            _isSprinting = false;

        _isSneaking = wantSneak;
        if (_isSneaking) _isSprinting = false;

        // --- Aktuelle Geschwindigkeit ---
        float currentMoveSpeed = _isSprinting ? SprintSpeed
                               : _isSneaking  ? SneakSpeed
                               : WalkSpeed;

        // --- Bewegungsrichtung berechnen ---
        Vector3 moveDirection = Vector3.Zero;

        Vector3 forward = Camera.Forward;
        forward.Y = 0;
        if (forward.LengthSquared() > 0)
            forward = Vector3.Normalize(forward);

        Vector3 right = Camera.Right;
        right.Y = 0;
        if (right.LengthSquared() > 0)
            right = Vector3.Normalize(right);

        if (keyState.IsKeyDown(Keys.W)) moveDirection += forward;
        if (keyState.IsKeyDown(Keys.S)) moveDirection -= forward;
        if (keyState.IsKeyDown(Keys.A)) moveDirection -= right;
        if (keyState.IsKeyDown(Keys.D)) moveDirection += right;

        if (_isGrounded)
        {
            // Am Boden: direkte Geschwindigkeitssetzung (sofortige Reaktion wie in Minecraft)
            if (moveDirection.LengthSquared() > 0)
            {
                moveDirection = Vector3.Normalize(moveDirection);
                Velocity.X = moveDirection.X * currentMoveSpeed;
                Velocity.Z = moveDirection.Z * currentMoveSpeed;
            }
            else
            {
                Velocity.X = 0f;
                Velocity.Z = 0f;
            }
        }
        else
        {
            // In der Luft: nur kleine Beschleunigung, kein direktes Umlenken
            // Minecraft: 0.02 b/tick Luftbeschleunigung (Sprint: 0.026 b/tick)
            if (moveDirection.LengthSquared() > 0)
            {
                moveDirection = Vector3.Normalize(moveDirection);
                float airAccel = (_isSprinting ? 0.026f : 0.02f) * 20f * 20f * deltaTime;
                Velocity.X += moveDirection.X * airAccel;
                Velocity.Z += moveDirection.Z * airAccel;

                // Horizontalgeschwindigkeit auf currentMoveSpeed begrenzen
                float horizSpeedSq = Velocity.X * Velocity.X + Velocity.Z * Velocity.Z;
                if (horizSpeedSq > currentMoveSpeed * currentMoveSpeed)
                {
                    float scale = currentMoveSpeed / (float)System.Math.Sqrt(horizSpeedSq);
                    Velocity.X *= scale;
                    Velocity.Z *= scale;
                }
            }
            // Kein Stopp in der Luft – Impuls wird durch Drag in PhysicsEngine abgebaut
        }

        // --- Springen ---
        if (_jumpBuffered && !_isSneaking)
        {
            if (_isGrounded && _jumpCooldown <= 0)
            {
                Velocity.Y = JumpStrength;
                _isGrounded = false;
                _jumpCooldown = JumpCooldownTime;
                _fallDistance = 0f;
                _jumpBuffered    = false;
                _jumpBufferTimer = 0f;
            }
            // Buffer bleibt aktiv bis Timer abläuft (siehe Tick) — kein sofortiges Leeren
        }

        _lastKeyState = keyState;
        _lastMouseState = mouseState;
    }

    public void StopHorizontalMovement()
    {
        Velocity.X = 0f;
        Velocity.Z = 0f;
    }

    public void TakeDamage(float damage)
    {
        Health -= damage;
        if (Health < 0)
            Health = 0;
    }

    public void Heal(float amount)
    {
        Health += amount;
        if (Health > MaxHealth)
            Health = MaxHealth;
    }

    public bool TryBreakBlock(World.World world, out Vector3 brokenBlock, out BlockType brokenBlockType)
    {
        brokenBlock = Vector3.Zero;
        brokenBlockType = BlockType.Air;

        if (Raycast.CastRay(world, Camera.Position, Camera.Forward, 5f,
            out Vector3 hitBlock, out Vector3 adjacentBlock))
        {
            brokenBlockType = world.GetBlock((int)hitBlock.X, (int)hitBlock.Y, (int)hitBlock.Z);
            world.SetBlock((int)hitBlock.X, (int)hitBlock.Y, (int)hitBlock.Z, BlockType.Air);
            brokenBlock = hitBlock;
            return true;
        }

        return false;
    }

    public void Respawn()
    {
        Position = SpawnPoint;
        PreviousPosition = SpawnPoint;
        RenderPosition = SpawnPoint;
        Velocity = Vector3.Zero;
        _fallDistance = 0f;
        _isGrounded = false;
        Health = MaxHealth;
    }

    public bool TryPlaceBlock(World.World world, BlockType blockType, out Vector3 placedBlock)
    {
        placedBlock = Vector3.Zero;

        if (Raycast.CastRay(world, Camera.Position, Camera.Forward, 5f,
            out Vector3 hitBlock, out Vector3 adjacentBlock))
        {
            Vector3 blockPos = adjacentBlock;
            AABB blockBox = new AABB(
                new Vector3(blockPos.X, blockPos.Y, blockPos.Z),
                new Vector3(blockPos.X + 1, blockPos.Y + 1, blockPos.Z + 1)
            );

            // Physik-Position verwenden (nicht Kamera) — beim Fallen liegt die Kamera
            // hinter der echten Position, was falsche Kollisionstests verursacht
            float currentHeight = _isSneaking ? SneakHeight : NormalHeight;
            AABB playerBox = AABB.FromPosition(Position, PlayerWidth, currentHeight, PlayerDepth);

            if (!blockBox.Intersects(playerBox))
            {
                world.SetBlock((int)adjacentBlock.X, (int)adjacentBlock.Y, (int)adjacentBlock.Z, blockType);
                placedBlock = adjacentBlock;
                return true;
            }
        }

        return false;
    }
}
