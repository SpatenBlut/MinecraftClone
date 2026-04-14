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

    public bool IsSprinting => _isSprinting;
    public bool IsSneaking => _isSneaking;

    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isSneaking;
    private float _jumpCooldown = 0f;

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
    private bool _jumpBuffered = false;

    // Für Render-Interpolation zwischen Ticks
    public Vector3 PreviousPosition;
    public Vector3 RenderPosition;
    private float _currentEyeHeight = NormalEyeHeight;

    // Smooth Sneak-Übergang (nicht instant wie in Minecraft)
    private float _smoothEyeHeight = NormalEyeHeight;
    private const float EyeHeightSpeed = 10f; // Einheiten/Sekunde

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
    public void Tick(float deltaTime, World.World world)
    {
        if (_jumpCooldown > 0)
            _jumpCooldown -= deltaTime;

        HandleInput(deltaTime, world);

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

        Vector3 newVelocity = Velocity;
        Position = PhysicsEngine.ApplyPhysics(Position, newVelocity, CollisionBox, world, deltaTime,
            _isGrounded, out _isGrounded, ref newVelocity, PlayerWidth, currentHeight, PlayerDepth);
        Velocity = newVelocity;

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
            _smoothEyeHeight = MathF.Min(_smoothEyeHeight + step, _currentEyeHeight);
        else if (_smoothEyeHeight > _currentEyeHeight)
            _smoothEyeHeight = MathF.Max(_smoothEyeHeight - step, _currentEyeHeight);

        Camera.IsSprinting = _isSprinting;
        Camera.Position = RenderPosition + new Vector3(0, _smoothEyeHeight, 0);
    }

    // Jeden Frame aufrufen — buffert kurze Tastendrücke die zwischen Ticks liegen könnten
    public void PollInput()
    {
        var keyState = Keyboard.GetState();
        if (keyState.IsKeyDown(Keys.Space) && _lastFrameKeyState.IsKeyUp(Keys.Space))
            _jumpBuffered = true;
        _lastFrameKeyState = keyState;
    }

    public void UpdateCamera(GameTime gameTime, GraphicsDevice graphicsDevice)
    {
        Camera.Update(gameTime, graphicsDevice);
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
        // Nutzt _isGrounded (Physics-State) statt einer frischen Probe – verhindert
        // das kurze Velocity-Setzen wenn der Spieler knapp über dem Boden ist.
        if (_jumpBuffered && !_isSneaking)
        {
            if (_isGrounded && _jumpCooldown <= 0)
            {
                Velocity.Y = JumpStrength;
                _isGrounded = false;
                _jumpCooldown = JumpCooldownTime;
            }
            _jumpBuffered = false;
        }

        _lastKeyState = keyState;
        _lastMouseState = mouseState;
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

    public bool TryBreakBlock(World.World world, out Vector3 brokenBlock)
    {
        brokenBlock = Vector3.Zero;

        if (Raycast.CastRay(world, Camera.Position, Camera.Forward, 5f,
            out Vector3 hitBlock, out Vector3 adjacentBlock))
        {
            world.SetBlock((int)hitBlock.X, (int)hitBlock.Y, (int)hitBlock.Z, BlockType.Air);
            brokenBlock = hitBlock;
            return true;
        }

        return false;
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

            // Konsistente Basis: Kamera-Position minus Augenhöhe (gleiche Basis wie der Raycast)
            Vector3 basePos = Camera.Position - new Vector3(0, _currentEyeHeight, 0);
            float currentHeight = _isSneaking ? SneakHeight : NormalHeight;
            AABB playerBox = AABB.FromPosition(basePos, PlayerWidth, currentHeight, PlayerDepth);

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
