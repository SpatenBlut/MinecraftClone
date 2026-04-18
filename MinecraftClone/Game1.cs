using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Core;
using MinecraftClone.Gameplay;
using MinecraftClone.Rendering;
using MinecraftClone.UI;
using MinecraftClone.World;

namespace MinecraftClone;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private World.World _world;
    private Player      _player;
    private Inventory   _inventory;
    private ChunkMesh   _chunkMesh;
    private HUD         _hud;
    private PauseMenu   _pauseMenu;

    private BlockEffect    _basicEffect;
    private BlockOutline   _blockOutline;
    private PlayerArm      _playerArm;
    private PlayerHeldItem _playerHeldItem;
    private PlayerModel    _playerModel;
    private Texture2D      _blockAtlas;
    private SpriteFont   _font;

    private Vector3? _targetBlock;

    // Sky
    private Texture2D _skyGradient;
    private Texture2D _sunTexture;
    private static readonly Color SkyZenith  = new Color(120, 167, 255);
    private static readonly Color SkyHorizon = new Color(192, 216, 255);
    private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(0.5f, 0.85f, 0.3f));

    // State
    private bool _needsMeshRebuild = false;
    private bool _inventoryOpen    = false;
    private bool _paused           = false;

    // Third-person body rotation (smoothed, lags behind camera yaw like MC)
    private float _bodyYaw = 0f;

    // Mining
    private Vector3? _miningTarget   = null;
    private float    _miningProgress = 0f;
    private float    _miningSwingTimer = 0f;
    private const float MiningSwingInterval = 0.2f;

    private KeyboardState _lastKeyState;
    private MouseState    _lastMouseState;

    // Stats
    private int    _blocksPlaced = 0;
    private int    _blocksBroken = 0;
    private double _playTimeSec  = 0.0;

    // 20 TPS Tick-System
    private const double TickInterval = 1.0 / 20.0;
    private double _tickAccumulator = 0.0;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";

        _graphics.IsFullScreen = true;
        _graphics.HardwareModeSwitch = true;
        _graphics.PreferredBackBufferWidth  = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        _graphics.SynchronizeWithVerticalRetrace = false;
        _graphics.ApplyChanges();

        IsMouseVisible  = false;
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        string worldPath = Path.Combine(Content.RootDirectory, "Worlds", "world1.dat");

        if (File.Exists(worldPath))
        {
            _world = World.World.LoadFromFile(worldPath);
        }
        else
        {
            _world = new World.World(64, 32, 64);
            _world.Generate();
            Directory.CreateDirectory(Path.GetDirectoryName(worldPath));
            _world.SaveToFile(worldPath);
        }

        _player    = new Player(new Vector3(32, 10, 32), GraphicsDevice.Viewport.AspectRatio);
        _inventory = new Inventory();
        _chunkMesh = new ChunkMesh(GraphicsDevice);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _basicEffect = new BlockEffect(Content.Load<Effect>("BasicShader"));
        _blockAtlas  = Content.Load<Texture2D>("Textures/atlas");
        _sunTexture  = Content.Load<Texture2D>("Textures/sun");
        _font        = Content.Load<SpriteFont>("Font");
        _hud          = new HUD(GraphicsDevice, _font, _blockAtlas);
        _pauseMenu    = new PauseMenu(GraphicsDevice, _font);
        _blockOutline = new BlockOutline(GraphicsDevice);
        _playerArm      = new PlayerArm(GraphicsDevice);
        _playerHeldItem = new PlayerHeldItem(GraphicsDevice, _blockAtlas);
        _playerModel    = new PlayerModel(GraphicsDevice);

        _skyGradient = new Texture2D(GraphicsDevice, 1, 2);
        _skyGradient.SetData(new[] { SkyZenith, SkyHorizon });

        _chunkMesh.Build(_world);

        _player.Camera.MouseSensitivity = _pauseMenu.MouseSensitivity;
        _player.Camera.BaseFov          = _pauseMenu.Fov;
        _pauseMenu.VSync                = _graphics.SynchronizeWithVerticalRetrace;
    }

    protected override void Update(GameTime gameTime)
    {
        var keyState   = Keyboard.GetState();
        var mouseState = Mouse.GetState();

        // ── ESC handling (edge-triggered) ─────────────────────────────────────
        if (keyState.IsKeyDown(Keys.Escape) && _lastKeyState.IsKeyUp(Keys.Escape))
        {
            if (_inventoryOpen)
            {
                _inventoryOpen = false;
                _inventory.ReturnCursorStack();
                _player.Camera.ResetMouseLock();
            }
            else if (!_paused)
            {
                _paused = true;
                _pauseMenu.MouseSensitivity = _player.Camera.MouseSensitivity;
                _pauseMenu.Fov              = _player.Camera.BaseFov;
                _pauseMenu.Open();
                // Consume this ESC press so PauseMenu.Update() doesn't see it
                // as a "resume" edge on the very same frame
                _lastKeyState   = keyState;
                _lastMouseState = mouseState;
                base.Update(gameTime);
                return;
            }
            // If already paused: PauseMenu.Update() handles ESC below
        }

        // ── Window focus lost ─────────────────────────────────────────────────
        if (!IsActive)
        {
            IsMouseVisible = true;
            _player.Camera.ResetMouseLock();
            _lastKeyState   = keyState;
            _lastMouseState = mouseState;
            base.Update(gameTime);
            return;
        }

        // ── Mouse visibility ──────────────────────────────────────────────────
        IsMouseVisible = _inventoryOpen || _paused;

        // ── Menu overlay (game keeps running) ────────────────────────────────
        if (_paused)
        {
            _pauseMenu.Update(_blocksPlaced, _blocksBroken, (float)_playTimeSec,
                mouseState, _lastMouseState, keyState, _lastKeyState);

            if (_pauseMenu.WantsResume)
            {
                _paused = false;
                _player.Camera.ResetMouseLock();
            }
            else if (_pauseMenu.WantsMainMenu || _pauseMenu.WantsQuit)
            {
                Exit();
            }

            _player.Camera.MouseSensitivity = _pauseMenu.MouseSensitivity;
            _player.Camera.BaseFov          = _pauseMenu.Fov;
            if (_pauseMenu.VSync != _graphics.SynchronizeWithVerticalRetrace)
            {
                _graphics.SynchronizeWithVerticalRetrace = _pauseMenu.VSync;
                _graphics.ApplyChanges();
            }
        }

        // ── E key: inventory ──────────────────────────────────────────────────
        if (!_paused && keyState.IsKeyDown(Keys.E) && _lastKeyState.IsKeyUp(Keys.E))
        {
            _inventoryOpen = !_inventoryOpen;
            if (!_inventoryOpen)
            {
                _inventory.ReturnCursorStack();
                _player.Camera.ResetMouseLock();
            }
        }

        // ── Play time ─────────────────────────────────────────────────────────
        _playTimeSec += gameTime.ElapsedGameTime.TotalSeconds;

        // ── Inventory open ────────────────────────────────────────────────────
        if (_inventoryOpen && !_paused)
        {
            int slotIdx = _hud.GetInventorySlotAt(
                mouseState.X, mouseState.Y,
                GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            bool shiftHeld = keyState.IsKeyDown(Keys.LeftShift) || keyState.IsKeyDown(Keys.RightShift);

            if (mouseState.LeftButton  == ButtonState.Pressed && _lastMouseState.LeftButton  == ButtonState.Released)
                if (slotIdx >= 0) _inventory.OnSlotLeftClick(slotIdx, shiftHeld);
            if (mouseState.RightButton == ButtonState.Pressed && _lastMouseState.RightButton == ButtonState.Released)
                if (slotIdx >= 0) _inventory.OnSlotRightClick(slotIdx);
        }

        // ── Physics / movement (always runs) ─────────────────────────────────
        bool menuActive = _paused || _inventoryOpen;
        if (!menuActive)
            _player.PollInput();

        _tickAccumulator += gameTime.ElapsedGameTime.TotalSeconds;
        int tickCount = 0;
        while (_tickAccumulator >= TickInterval && tickCount < 5)
        {
            _player.SaveTickState();
            _player.Tick((float)TickInterval, _world, inputEnabled: !menuActive);
            if (!menuActive)
                _inventory.Update(keyState, mouseState.ScrollWheelValue);
            _tickAccumulator -= TickInterval;
            tickCount++;
        }

        float interpAlpha = (float)(_tickAccumulator / TickInterval);
        _player.SetRenderPosition(interpAlpha, (float)gameTime.ElapsedGameTime.TotalSeconds);
        _player.UpdateCamera(gameTime, GraphicsDevice, captureMouseInput: !menuActive);

        // ── Body yaw — MC-faithful delayed body rotation ─────────────────────
        {
            float dt   = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float diff = WrapAngle(_player.Camera.Yaw - _bodyYaw);
            bool  moving = MathF.Abs(_player.Velocity.X) > 0.01f
                        || MathF.Abs(_player.Velocity.Z) > 0.01f;
            if (moving)
            {
                // Smooth follow while walking (MC: bodyYaw += diff * 0.3 per tick)
                _bodyYaw += diff * 6f * dt;
            }
            else
            {
                // Standing: snap only when head exceeds 75° offset from body
                const float MaxOff = 75f * MathHelper.Pi / 180f;
                if (MathF.Abs(diff) > MaxOff)
                    _bodyYaw += diff - MathF.Sign(diff) * MaxOff;
            }
        }

        // ── Zielblock bestimmen (jeden Frame) ────────────────────────────────
        if (!menuActive && Raycast.CastRay(_world, _player.Camera.Position,
                _player.Camera.Forward, 5f, out Vector3 hb, out _))
            _targetBlock = hb;
        else
            _targetBlock = null;

        // ── Block interaction (only when playing freely) ──────────────────────
        if (!menuActive)
        {
            float frameDt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (mouseState.LeftButton == ButtonState.Pressed && _targetBlock.HasValue)
            {
                var tb = _targetBlock.Value;
                if (_miningTarget != tb)
                {
                    _miningTarget      = tb;
                    _miningProgress    = 0f;
                    _miningSwingTimer  = 0f;
                    _player.TriggerSwing();
                }

                _miningProgress   += frameDt;
                _miningSwingTimer -= frameDt;
                if (_miningSwingTimer <= 0f)
                {
                    _player.TriggerSwing();
                    _miningSwingTimer = MiningSwingInterval;
                }

                BlockType bt = _world.GetBlock((int)tb.X, (int)tb.Y, (int)tb.Z);
                if (_miningProgress >= GetBreakTime(bt))
                {
                    _world.SetBlock((int)tb.X, (int)tb.Y, (int)tb.Z, BlockType.Air);
                    _blocksBroken++;
                    _needsMeshRebuild = true;
                    _miningTarget     = null;
                    _miningProgress   = 0f;
                    _miningSwingTimer = 0f;
                }
            }
            else
            {
                if (mouseState.LeftButton == ButtonState.Pressed
                    && _lastMouseState.LeftButton == ButtonState.Released)
                    _player.TriggerSwing(); // swing in air on fresh press
                _miningTarget   = null;
                _miningProgress = 0f;
            }

            if (mouseState.RightButton == ButtonState.Pressed && _lastMouseState.RightButton == ButtonState.Released)
            {
                if (_player.TryPlaceBlock(_world, _inventory.SelectedBlock, out _))
                {
                    _player.TriggerSwing();
                    _inventory.ConsumeFromSelected();
                    _blocksPlaced++;
                    _needsMeshRebuild = true;
                }
            }
        }

        _lastMouseState = mouseState;

        if (_needsMeshRebuild)
        {
            _chunkMesh.Build(_world);
            _needsMeshRebuild = false;
        }

        if (keyState.IsKeyDown(Keys.F5) && _lastKeyState.IsKeyUp(Keys.F5))
        {
            _player.Camera.Mode = _player.Camera.Mode switch
            {
                CameraMode.FirstPerson     => CameraMode.ThirdPersonBack,
                CameraMode.ThirdPersonBack => CameraMode.ThirdPersonFront,
                _                          => CameraMode.FirstPerson,
            };
        }

        if (keyState.IsKeyDown(Keys.F6) && _lastKeyState.IsKeyUp(Keys.F6))
        {
            string worldPath = Path.Combine(Content.RootDirectory, "Worlds", "world1.dat");
            _world.SaveToFile(worldPath);
        }

        _lastKeyState = keyState;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        int screenW = GraphicsDevice.Viewport.Width;
        int screenH = GraphicsDevice.Viewport.Height;

        GraphicsDevice.Clear(Color.Black);

        // Sky gradient
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        _spriteBatch.Draw(_skyGradient, new Rectangle(0, 0, screenW, screenH), Color.White);
        _spriteBatch.End();

        // Sun (additive)
        _spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
        DrawSun2D(screenW, screenH);
        _spriteBatch.End();

        // 3D world
        GraphicsDevice.BlendState        = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState   = RasterizerState.CullCounterClockwise;

        _basicEffect.World          = Matrix.Identity;
        _basicEffect.View           = _player.Camera.ViewMatrix;
        _basicEffect.Projection     = _player.Camera.ProjectionMatrix;
        _basicEffect.Texture        = _blockAtlas;
        _basicEffect.CameraPosition = _player.Camera.ViewPosition;
        _basicEffect.DayBrightness  = 1.0f;
        _basicEffect.FogColor       = SkyHorizon.ToVector3();
        _basicEffect.FogStart       = 48f;
        _basicEffect.FogEnd         = 90f;
        _basicEffect.Apply();

        _chunkMesh.Draw();

        // Block-Outline
        if (_targetBlock.HasValue)
            _blockOutline.Draw(_targetBlock.Value, _player.Camera.Position,
                _player.Camera.ViewMatrix, _player.Camera.ProjectionMatrix);

        // Player model (third person only)
        if (_player.Camera.Mode != CameraMode.FirstPerson)
            _playerModel.Draw(_player.RenderPosition,
                _bodyYaw, _player.Camera.Yaw, _player.Camera.Pitch,
                _player.Camera.ViewMatrix, _player.Camera.ProjectionMatrix);

        // First-person hand — only in first person
        if (_player.Camera.Mode == CameraMode.FirstPerson)
        {
            GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, 1f, 0);
            var heldBlock = _inventory.SelectedBlock;
            if (heldBlock == BlockType.Air)
                _playerArm.Draw(_player.Camera, _player.SwingProgress);
            else
                _playerHeldItem.Draw(_player.Camera, heldBlock,
                    _pauseMenu.HandOffsetX, _pauseMenu.HandOffsetY,
                    _pauseMenu.HandOffsetZ, _pauseMenu.HandScale);
        }

        // HUD
        var ms = Mouse.GetState();
        _hud.Draw(_spriteBatch, _player, _inventory, screenW, screenH, gameTime,
            _inventoryOpen, ms.X, ms.Y);

        // Pause menu overlay
        if (_paused)
        {
            _spriteBatch.Begin();
            _pauseMenu.Draw(_spriteBatch, _player, screenW, screenH, ms.X, ms.Y);
            _spriteBatch.End();
        }

        base.Draw(gameTime);
    }

    private void DrawSun2D(int screenW, int screenH)
    {
        Vector3 sunWorldPos = _player.Camera.ViewPosition + SunDirection * 500f;
        Matrix viewProj = _player.Camera.ViewMatrix * _player.Camera.ProjectionMatrix;
        Vector4 clip = Vector4.Transform(new Vector4(sunWorldPos, 1f), viewProj);

        if (clip.W <= 0f) return;

        float ndcX =  clip.X / clip.W;
        float ndcY = -clip.Y / clip.W;

        if (ndcX < -1.5f || ndcX > 1.5f || ndcY < -1.5f || ndcY > 1.5f) return;

        int sx = (int)((ndcX + 1f) * 0.5f * screenW);
        int sy = (int)((ndcY + 1f) * 0.5f * screenH);

        const int sunSize = 160;
        _spriteBatch.Draw(_sunTexture,
            new Rectangle(sx - sunSize / 2, sy - sunSize / 2, sunSize, sunSize),
            Color.White);
    }

    private static float WrapAngle(float r)
    {
        r %= MathHelper.TwoPi;
        if (r >  MathHelper.Pi) r -= MathHelper.TwoPi;
        if (r < -MathHelper.Pi) r += MathHelper.TwoPi;
        return r;
    }

    // Minecraft bare-hand break times: hardness * 1.5 seconds
    private static float GetBreakTime(BlockType block) => block switch
    {
        BlockType.Leaves => 0.3f,
        BlockType.Sand   => 0.75f,
        BlockType.Dirt   => 0.75f,
        BlockType.Grass  => 0.9f,
        BlockType.Stone  => 2.25f,
        BlockType.Wood   => 3.0f,
        BlockType.Water  => float.MaxValue,
        _                => 1.5f,
    };
}
