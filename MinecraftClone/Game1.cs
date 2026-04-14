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
        _graphics.HardwareModeSwitch = true; // Exclusive Fullscreen (kein Borderless)
        _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        _graphics.ApplyChanges();

        IsMouseVisible = false;

        // Rendering läuft uncapped, Logik läuft via manuellem 20-TPS-Accumulator
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        // Welt erstellen oder laden
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

        // Spieler initialisieren
        float aspectRatio = GraphicsDevice.Viewport.AspectRatio;
        _player = new Player(new Vector3(32, 10, 32), aspectRatio);

        // Inventory
        _inventory = new Inventory();

        // Mesh
        _chunkMesh = new ChunkMesh(GraphicsDevice);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Basic Effect initialisieren
        _basicEffect = new BasicEffect3D(GraphicsDevice);

        // Textur-Atlas laden
        _blockAtlas = Content.Load<Texture2D>("Textures/atlas");

        // Font laden (MonoGame default)
        _font = Content.Load<SpriteFont>("Font");

        // HUD
        _hud = new HUD(GraphicsDevice, _font);

        // Initial Mesh bauen
        _chunkMesh.Build(_world);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyState = Keyboard.GetState();

        if (keyState.IsKeyDown(Keys.Escape))
            Exit();

        // Input jeden Frame pollen (damit kurze Tastendrücke zwischen Ticks nicht verloren gehen)
        _player.PollInput();

        // 20-TPS Tick-Accumulator (wie Minecraft)
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

        // Interpolations-Alpha (wie weit sind wir zwischen letztem und nächstem Tick)
        float alpha = (float)(_tickAccumulator / TickInterval);
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _player.SetRenderPosition(alpha, deltaTime);

        // Kamera (Mouse-Look) läuft jeden Frame für flüssige Rotation
        _player.UpdateCamera(gameTime, GraphicsDevice);

        // Block abbauen (linke Maustaste) - nur bei neuem Klick
        var mouseState = Mouse.GetState();
        if (mouseState.LeftButton == ButtonState.Pressed && _lastMouseState.LeftButton == ButtonState.Released)
        {
            if (_player.TryBreakBlock(_world, out Vector3 brokenBlock))
            {
                _needsMeshRebuild = true;
            }
        }

        // Block platzieren (rechte Maustaste) - nur bei neuem Klick
        if (mouseState.RightButton == ButtonState.Pressed && _lastMouseState.RightButton == ButtonState.Released)
        {
            if (_player.TryPlaceBlock(_world, _inventory.SelectedBlock, out Vector3 placedBlock))
            {
                _needsMeshRebuild = true;
            }
        }

        _lastMouseState = mouseState;

        // Mesh neu bauen wenn nötig
        if (_needsMeshRebuild)
        {
            _chunkMesh.Build(_world);
            _needsMeshRebuild = false;
        }

        // Welt speichern (F5)
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
        GraphicsDevice.Clear(new Color(135, 206, 235)); // Sky blue

        // 3D Rendering
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

        _basicEffect.World = Matrix.Identity;
        _basicEffect.View = _player.Camera.ViewMatrix;
        _basicEffect.Projection = _player.Camera.ProjectionMatrix;
        _basicEffect.Texture = _blockAtlas;

        _basicEffect.Apply();
        _chunkMesh.Draw();

        // 2D UI
        _hud.Draw(_spriteBatch, _player, _inventory,
            GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, gameTime);

        base.Draw(gameTime);
    }
}