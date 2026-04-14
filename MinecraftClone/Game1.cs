using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    private Player _player;
    private Inventory _inventory;
    private ChunkMesh _chunkMesh;
    private HUD _hud;

    private BasicEffect3D _basicEffect;
    private Texture2D _blockAtlas;
    private SpriteFont _font;

    // Sky
    private Texture2D _skyGradient; // 1x2: Zenit (oben) → Horizont (unten)
    private Texture2D _sunTexture;
    private static readonly Color SkyZenith  = new Color(100, 145, 230); // tiefes Blau oben
    private static readonly Color SkyHorizon = new Color(160, 195, 255); // helles Blau am Horizont
    private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(0.5f, 0.85f, 0.3f));

    private bool _needsMeshRebuild = false;
    private KeyboardState _lastKeyState;
    private MouseState _lastMouseState;

    // 20 TPS Tick-System (wie Minecraft)
    private const double TickInterval = 1.0 / 20.0; // 50ms pro Tick
    private double _tickAccumulator = 0.0;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";

        // Fullscreen Exclusive Mode
        _graphics.IsFullScreen = true;
        _graphics.HardwareModeSwitch = true;
        _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        _graphics.ApplyChanges();

        IsMouseVisible = false;
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

        float aspectRatio = GraphicsDevice.Viewport.AspectRatio;
        _player = new Player(new Vector3(32, 10, 32), aspectRatio);
        _inventory = new Inventory();
        _chunkMesh = new ChunkMesh(GraphicsDevice);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _basicEffect = new BasicEffect3D(GraphicsDevice);
        _blockAtlas  = Content.Load<Texture2D>("Textures/atlas");
        _sunTexture  = Content.Load<Texture2D>("Textures/sun");
        _font        = Content.Load<SpriteFont>("Font");
        _hud         = new HUD(GraphicsDevice, _font);

        // Sky-Gradient: 1×2 Pixel — bilineares Strecken ergibt sauberen Farbverlauf
        _skyGradient = new Texture2D(GraphicsDevice, 1, 2);
        _skyGradient.SetData(new[] { SkyZenith, SkyHorizon });

        _chunkMesh.Build(_world);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyState = Keyboard.GetState();

        if (keyState.IsKeyDown(Keys.Escape))
            Exit();

        _player.PollInput();

        _tickAccumulator += gameTime.ElapsedGameTime.TotalSeconds;

        int ticksThisFrame = 0;
        while (_tickAccumulator >= TickInterval && ticksThisFrame < 5)
        {
            _player.SaveTickState();
            _player.Tick((float)TickInterval, _world);
            _inventory.Update();
            _tickAccumulator -= TickInterval;
            ticksThisFrame++;
        }

        float alpha     = (float)(_tickAccumulator / TickInterval);
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _player.SetRenderPosition(alpha, deltaTime);
        _player.UpdateCamera(gameTime, GraphicsDevice);

        var mouseState = Mouse.GetState();
        if (mouseState.LeftButton == ButtonState.Pressed && _lastMouseState.LeftButton == ButtonState.Released)
        {
            if (_player.TryBreakBlock(_world, out _))
                _needsMeshRebuild = true;
        }

        if (mouseState.RightButton == ButtonState.Pressed && _lastMouseState.RightButton == ButtonState.Released)
        {
            if (_player.TryPlaceBlock(_world, _inventory.SelectedBlock, out _))
                _needsMeshRebuild = true;
        }

        _lastMouseState = mouseState;

        if (_needsMeshRebuild)
        {
            _chunkMesh.Build(_world);
            _needsMeshRebuild = false;
        }

        if (keyState.IsKeyDown(Keys.F5) && _lastKeyState.IsKeyUp(Keys.F5))
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

        // 1) Farbpuffer + Tiefenpuffer leeren
        GraphicsDevice.Clear(Color.Black);

        // 2a) Sky-Gradient
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        _spriteBatch.Draw(_skyGradient, new Rectangle(0, 0, screenW, screenH), Color.White);
        _spriteBatch.End();

        // 2b) Sonne mit Additive Blending (kein schwarzes Kästchen, so wie Minecraft)
        _spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
        DrawSun2D(screenW, screenH);
        _spriteBatch.End();

        // 3) 3D Welt (überschreibt Sky wo Geometrie ist)
        GraphicsDevice.BlendState        = BlendState.Opaque;           // nach Additive-SpriteBatch zurücksetzen
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState   = RasterizerState.CullCounterClockwise;

        _basicEffect.World      = Matrix.Identity;
        _basicEffect.View       = _player.Camera.ViewMatrix;
        _basicEffect.Projection = _player.Camera.ProjectionMatrix;
        _basicEffect.Texture    = _blockAtlas;

        // Fog zur Horizont-Farbe hin (Minecraft Render-Distance-Effekt)
        _basicEffect.FogEnabled = true;
        _basicEffect.FogColor   = SkyHorizon.ToVector3();
        _basicEffect.FogStart   = 40f;
        _basicEffect.FogEnd     = 62f;

        _basicEffect.Apply();
        _chunkMesh.Draw();

        // 4) HUD
        _hud.Draw(_spriteBatch, _player, _inventory, screenW, screenH, gameTime);

        base.Draw(gameTime);
    }

    // Sonne als 2D-Projektion aus 3D-Richtungsvektor
    private void DrawSun2D(int screenW, int screenH)
    {
        Vector3 sunWorldPos = _player.Camera.Position + SunDirection * 500f;
        Matrix viewProj = _player.Camera.ViewMatrix * _player.Camera.ProjectionMatrix;
        Vector4 clip = Vector4.Transform(new Vector4(sunWorldPos, 1f), viewProj);

        if (clip.W <= 0f) return; // hinter der Kamera

        float ndcX =  clip.X / clip.W;
        float ndcY = -clip.Y / clip.W;

        // Nur zeichnen wenn annähernd auf dem Bildschirm
        if (ndcX < -1.5f || ndcX > 1.5f || ndcY < -1.5f || ndcY > 1.5f) return;

        int sx = (int)((ndcX + 1f) * 0.5f * screenW);
        int sy = (int)((ndcY + 1f) * 0.5f * screenH);

        const int sunSize = 160;
        _spriteBatch.Draw(_sunTexture,
            new Rectangle(sx - sunSize / 2, sy - sunSize / 2, sunSize, sunSize),
            Color.White);
    }
}
