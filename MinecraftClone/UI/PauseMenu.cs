using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Gameplay;

namespace MinecraftClone.UI;

public class PauseMenu
{
    private enum Screen
    {
        Main, Settings, Stats,
        Graphics, RenderQuality, Controls
    }

    private enum DragTarget
    {
        None,
        Sensitivity,
        Fov,
        HandX, HandY, HandZ, HandScale,
        RenderDist
    }

    private Screen     _screen;
    private DragTarget _dragging = DragTarget.None;

    private readonly SpriteFont     _font;
    private readonly Texture2D      _pixel;
    private readonly GraphicsDevice _gd;

    private readonly Dictionary<int, Texture2D> _fillCornerCache    = new();
    private readonly Dictionary<int, Texture2D> _outlineCornerCache = new();

    // ── Dark-Steel palette ────────────────────────────────────────────────────
    private static readonly Color ColOverlay = new Color(0,   0,   0,   185);
    private static readonly Color ColPanel   = new Color(26,  30,  38);
    private static readonly Color ColBtn     = new Color(42,  48,  60);
    private static readonly Color ColBtnHov  = new Color(60,  68,  86);
    private static readonly Color ColBtnRed  = new Color(70,  26,  26);
    private static readonly Color ColBtnRedH = new Color(98,  36,  36);
    private static readonly Color ColLight   = new Color(88,  100, 120);
    private static readonly Color ColDark    = new Color(12,  14,  18);
    private static readonly Color ColText    = new Color(215, 225, 235);
    private static readonly Color ColMuted   = new Color(138, 152, 168);
    private static readonly Color ColAccent  = new Color(96,  176, 255);
    private static readonly Color ColSep     = new Color(55,  62,  78);

    // ── Sensitivity ───────────────────────────────────────────────────────────
    // Stored as Minecraft % (0-200), default 16
    private float _sensPct = 16f;

    public float MouseSensitivity
    {
        get => SensPctToRad(_sensPct);
        set => _sensPct = RadToSensPct(value);
    }

    private static float SensPctToRad(float pct)
    {
        float s = pct / 100f;
        float f = s * 0.6f + 0.2f;
        return f * f * f * 8.0f * 0.15f * (MathF.PI / 180f);
    }

    private static float RadToSensPct(float rad)
    {
        float cbrt = MathF.Cbrt(rad / (0.15f * (MathF.PI / 180f) * 8.0f));
        return Math.Clamp((cbrt - 0.2f) / 0.6f * 100f, 0f, 100f);
    }

    // ── Graphics settings ─────────────────────────────────────────────────────
    public float Fov   { get; set; } = 90f;
    public bool  VSync { get; set; } = false;

    // Hand / held-item position (camera-space, tuned defaults)
    public float HandOffsetX { get; set; } =  0.67f;
    public float HandOffsetY { get; set; } = -0.70f;
    public float HandOffsetZ { get; set; } = -1.28f;
    public float HandScale   { get; set; } =  0.33f;

    // ── Render Quality settings (placeholder — not yet wired) ─────────────────
    private float _renderDist    = 64f;
    private bool  _smoothLighting = true;

    // ── Single-frame intent flags ─────────────────────────────────────────────
    public bool WantsResume   { get; private set; }
    public bool WantsMainMenu { get; private set; }
    public bool WantsQuit     { get; private set; }

    private int   _blocksPlaced;
    private int   _blocksBroken;
    private float _playTimeSec;

    // ── Layout constants ──────────────────────────────────────────────────────
    private const int PW   = 440;
    private const int BW   = 362;
    private const int BH   = 54;
    private const int BGap = 10;
    private const int TPad = 28;
    private const int TH   = 54;
    private const int RowH = 62;
    private const int StH  = 36;

    // ── Font scales ───────────────────────────────────────────────────────────
    private const float FsTitle = 1.56f;
    private const float FsBtn   = 1.04f;
    private const float FsLabel = 0.84f;
    private const float FsValue = 0.80f;
    private const float FsStat  = 0.76f;
    private const float FsSub   = 0.72f;

    public PauseMenu(GraphicsDevice gd, SpriteFont font)
    {
        _gd   = gd;
        _font = font;
        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Open()
    {
        _screen   = Screen.Main;
        _dragging = DragTarget.None;
        WantsResume = WantsMainMenu = WantsQuit = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public void Update(int blocksPlaced, int blocksBroken, float playTimeSec,
                       MouseState ms, MouseState lastMs,
                       KeyboardState ks, KeyboardState lastKs)
    {
        _blocksPlaced = blocksPlaced;
        _blocksBroken = blocksBroken;
        _playTimeSec  = playTimeSec;
        WantsResume = WantsMainMenu = WantsQuit = false;

        if (ks.IsKeyDown(Keys.Escape) && lastKs.IsKeyUp(Keys.Escape))
        {
            if (_screen != Screen.Main) _screen = Screen.Main;
            else WantsResume = true;
            return;
        }

        int mx = ms.X, my = ms.Y;
        int sw = _gd.Viewport.Width, sh = _gd.Viewport.Height;

        bool clicked  = ms.LeftButton == ButtonState.Pressed  && lastMs.LeftButton == ButtonState.Released;
        bool held     = ms.LeftButton == ButtonState.Pressed;
        bool released = ms.LeftButton == ButtonState.Released  && lastMs.LeftButton == ButtonState.Pressed;

        switch (_screen)
        {
            case Screen.Main:          if (clicked) HandleMain(mx, my, sw, sh);                         break;
            case Screen.Settings:      if (clicked) HandleSettings(mx, my, sw, sh);                     break;
            case Screen.Stats:         if (clicked) HandleStats(mx, my, sw, sh);                        break;
            case Screen.Graphics:      HandleGraphics(mx, my, sw, sh, clicked, held, released);         break;
            case Screen.RenderQuality: HandleRenderQuality(mx, my, sw, sh, clicked, held, released);    break;
            case Screen.Controls:      HandleControls(mx, my, sw, sh, clicked, held, released);         break;
        }
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, Player player, int sw, int sh, int mx, int my)
    {
        sb.Draw(_pixel, new Rectangle(0, 0, sw, sh), ColOverlay);
        switch (_screen)
        {
            case Screen.Main:          DrawMain(sb, sw, sh, mx, my);                break;
            case Screen.Settings:      DrawSettings(sb, sw, sh, mx, my);            break;
            case Screen.Stats:         DrawStats(sb, player, sw, sh, mx, my);       break;
            case Screen.Graphics:      DrawGraphics(sb, sw, sh, mx, my);            break;
            case Screen.RenderQuality: DrawRenderQuality(sb, sw, sh, mx, my);       break;
            case Screen.Controls:      DrawControls(sb, sw, sh, mx, my);            break;
        }
    }

    // ═══════════════════════════════ MAIN ════════════════════════════════════

    private static readonly (string Label, bool Danger)[] MainItems =
    {
        ("Main Menu",  false),
        ("Settings",   false),
        ("Statistics", false),
        ("Leave",      true),
    };

    private (int wx, int wy, int ph, Rectangle[] btns) MainLayout(int sw, int sh)
    {
        int ph = TPad + TH + MainItems.Length * BH + (MainItems.Length - 1) * BGap + TPad;
        int wx = (sw - PW) / 2, wy = (sh - ph) / 2;
        int bx = wx + (PW - BW) / 2;
        int by = wy + TPad + TH;
        var btns = new Rectangle[MainItems.Length];
        for (int i = 0; i < btns.Length; i++)
            btns[i] = new Rectangle(bx, by + i * (BH + BGap), BW, BH);
        return (wx, wy, ph, btns);
    }

    private void DrawMain(SpriteBatch sb, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph, btns) = MainLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "PAUSED", wx, wy);

        for (int i = 0; i < MainItems.Length; i++)
        {
            var (label, danger) = MainItems[i];
            bool hov = btns[i].Contains(mx, my);
            Color bg = danger ? (hov ? ColBtnRedH : ColBtnRed) : hov ? ColBtnHov : ColBtn;
            DrawButton(sb, btns[i], label, bg, ColText);
        }
    }

    private void HandleMain(int mx, int my, int sw, int sh)
    {
        var (_, _, _, btns) = MainLayout(sw, sh);
        if      (btns[0].Contains(mx, my)) WantsMainMenu = true;
        else if (btns[1].Contains(mx, my)) _screen       = Screen.Settings;
        else if (btns[2].Contains(mx, my)) _screen       = Screen.Stats;
        else if (btns[3].Contains(mx, my)) WantsQuit     = true;
    }

    // ══════════════════════════ SETTINGS (category list) ══════════════════════

    private static readonly string[] SettingsCategories = { "Graphics", "Render Quality", "Controls" };

    private (int wx, int wy, int ph, Rectangle[] btns, Rectangle back) SettingsLayout(int sw, int sh)
    {
        int n  = SettingsCategories.Length;
        int ph = TPad + TH + n * BH + (n - 1) * BGap + 20 + BH + TPad;
        int wx = (sw - PW) / 2, wy = (sh - ph) / 2;
        int bx = wx + (PW - BW) / 2;
        int by = wy + TPad + TH;
        var btns = new Rectangle[n];
        for (int i = 0; i < n; i++)
            btns[i] = new Rectangle(bx, by + i * (BH + BGap), BW, BH);
        var back = new Rectangle(bx, by + n * BH + (n - 1) * BGap + 20, BW, BH);
        return (wx, wy, ph, btns, back);
    }

    private void DrawSettings(SpriteBatch sb, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph, btns, back) = SettingsLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "SETTINGS", wx, wy);

        for (int i = 0; i < SettingsCategories.Length; i++)
            DrawButton(sb, btns[i], SettingsCategories[i],
                btns[i].Contains(mx, my) ? ColBtnHov : ColBtn, ColText);

        DrawButton(sb, back, "Back", back.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
    }

    private void HandleSettings(int mx, int my, int sw, int sh)
    {
        var (_, _, _, btns, back) = SettingsLayout(sw, sh);
        if      (btns[0].Contains(mx, my)) _screen = Screen.Graphics;
        else if (btns[1].Contains(mx, my)) _screen = Screen.RenderQuality;
        else if (btns[2].Contains(mx, my)) _screen = Screen.Controls;
        else if (back.Contains(mx, my))    _screen = Screen.Main;
    }

    // ══════════════════════════ GRAPHICS ══════════════════════════════════════
    // FOV, VSync, + Hand / held-item position sliders

    private (int wx, int wy, int ph) GraphicsLayout(int sw, int sh)
    {
        // 1 slider (FOV) + toggle row + sub-heading + 4 sliders (hand) + gap + Back
        int ph = TPad + TH
               + RowH              // FOV
               + (BH + BGap)       // VSync
               + 32                // sub-heading "Hand Item"
               + 4 * RowH          // hand X/Y/Scale/Z
               + 20 + BH + TPad;
        return ((sw - PW) / 2, (sh - ph) / 2, ph);
    }

    private void DrawGraphics(SpriteBatch sb, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph) = GraphicsLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "GRAPHICS", wx, wy);

        int bx = wx + (PW - BW) / 2;
        int ry = wy + TPad + TH;

        DrawSlider(sb, wx, ry, "Field of View", $"{Fov:F0}", (Fov - 30f) / 110f);
        ry += RowH;

        var vsync = new Rectangle(bx, ry, BW, BH);
        Color vsyncBg = VSync ? ColAccent * 0.6f : ColBtn;
        DrawButton(sb, vsync, VSync ? "VSync: ON" : "VSync: OFF",
            vsync.Contains(mx, my) ? (VSync ? ColAccent * 0.8f : ColBtnHov) : vsyncBg, ColText);
        ry += BH + BGap;

        DrawSubHeading(sb, wx, ry, "HAND ITEM");
        ry += 32;

        DrawSlider(sb, wx, ry, "Left / Right",   $"{HandOffsetX:F2}", (HandOffsetX + 2f) / 4f);
        ry += RowH;
        DrawSlider(sb, wx, ry, "Up / Down",       $"{HandOffsetY:F2}", (HandOffsetY + 2f) / 4f);
        ry += RowH;
        DrawSlider(sb, wx, ry, "Scale",           $"{HandScale:F2}x",  (HandScale - 0.05f) / 0.95f);
        ry += RowH;
        DrawSlider(sb, wx, ry, "Forward / Back",  $"{HandOffsetZ:F2}", (HandOffsetZ + 2f) / 2.5f);
        ry += RowH + 20;

        var back = new Rectangle(bx, ry, BW, BH);
        DrawButton(sb, back, "Back", back.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
    }

    private void HandleGraphics(int mx, int my, int sw, int sh,
                                bool clicked, bool held, bool released)
    {
        var (wx, wy, _) = GraphicsLayout(sw, sh);
        int bx  = wx + (PW - BW) / 2;
        int ry0 = wy + TPad + TH;                 // FOV slider
        int ryV = ry0 + RowH;                     // VSync button
        int ry1 = ryV + BH + BGap + 32;           // Hand X
        int ry2 = ry1 + RowH;                     // Hand Y
        int ry3 = ry2 + RowH;                     // Hand Scale
        int ry4 = ry3 + RowH;                     // Hand Z

        var fovTrack = SliderTrack(wx, ry0);
        var xTrack   = SliderTrack(wx, ry1);
        var yTrack   = SliderTrack(wx, ry2);
        var sTrack   = SliderTrack(wx, ry3);
        var zTrack   = SliderTrack(wx, ry4);

        var fovHit = new Rectangle(fovTrack.X, ry0 + 4, fovTrack.Width, RowH - 8);
        var xHit   = new Rectangle(xTrack.X,   ry1 + 4, xTrack.Width,   RowH - 8);
        var yHit   = new Rectangle(yTrack.X,   ry2 + 4, yTrack.Width,   RowH - 8);
        var sHit   = new Rectangle(sTrack.X,   ry3 + 4, sTrack.Width,   RowH - 8);
        var zHit   = new Rectangle(zTrack.X,   ry4 + 4, zTrack.Width,   RowH - 8);

        if (clicked)
        {
            if      (fovHit.Contains(mx, my))  _dragging = DragTarget.Fov;
            else if (xHit.Contains(mx, my))    _dragging = DragTarget.HandX;
            else if (yHit.Contains(mx, my))    _dragging = DragTarget.HandY;
            else if (sHit.Contains(mx, my))    _dragging = DragTarget.HandScale;
            else if (zHit.Contains(mx, my))    _dragging = DragTarget.HandZ;
        }

        if (released) _dragging = DragTarget.None;

        if (held)
        {
            if (_dragging == DragTarget.Fov)
            {
                float t = Math.Clamp((mx - fovTrack.X) / (float)fovTrack.Width, 0f, 1f);
                Fov = MathF.Round(30f + t * 110f);
            }
            else if (_dragging == DragTarget.HandX)
            {
                float t = Math.Clamp((mx - xTrack.X) / (float)xTrack.Width, 0f, 1f);
                HandOffsetX = MathF.Round((-2f + t * 4f) * 100f) / 100f;
            }
            else if (_dragging == DragTarget.HandY)
            {
                float t = Math.Clamp((mx - yTrack.X) / (float)yTrack.Width, 0f, 1f);
                HandOffsetY = MathF.Round((-2f + t * 4f) * 100f) / 100f;
            }
            else if (_dragging == DragTarget.HandScale)
            {
                float t = Math.Clamp((mx - sTrack.X) / (float)sTrack.Width, 0f, 1f);
                HandScale = MathF.Round((0.05f + t * 0.95f) * 100f) / 100f;
            }
            else if (_dragging == DragTarget.HandZ)
            {
                float t = Math.Clamp((mx - zTrack.X) / (float)zTrack.Width, 0f, 1f);
                HandOffsetZ = MathF.Round((-2f + t * 2.5f) * 100f) / 100f;
            }
        }

        if (clicked)
        {
            var vsync = new Rectangle(bx, ryV, BW, BH);
            if (vsync.Contains(mx, my)) VSync = !VSync;

            int backY = ry4 + RowH + 20;
            if (new Rectangle(bx, backY, BW, BH).Contains(mx, my))
            {
                _dragging = DragTarget.None;
                _screen   = Screen.Settings;
            }
        }
    }

    // ══════════════════════ RENDER QUALITY (placeholder) ══════════════════════

    private (int wx, int wy, int ph) RenderQualityLayout(int sw, int sh)
    {
        int ph = TPad + TH + RowH + (BH + BGap) + 20 + BH + TPad;
        return ((sw - PW) / 2, (sh - ph) / 2, ph);
    }

    private void DrawRenderQuality(SpriteBatch sb, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph) = RenderQualityLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "RENDER QUALITY", wx, wy);

        int bx = wx + (PW - BW) / 2;
        int ry = wy + TPad + TH;

        // Placeholder: Render Distance (not yet wired to game logic)
        float rdT = (_renderDist - 16f) / (256f - 16f);
        DrawSlider(sb, wx, ry, "Render Distance", $"{(int)_renderDist} chunks", rdT);
        ry += RowH;

        // Placeholder: Smooth Lighting toggle
        var smoothBtn = new Rectangle(bx, ry, BW, BH);
        Color sBg = _smoothLighting ? ColAccent * 0.6f : ColBtn;
        DrawButton(sb, smoothBtn, _smoothLighting ? "Smooth Lighting: ON" : "Smooth Lighting: OFF",
            smoothBtn.Contains(mx, my) ? (_smoothLighting ? ColAccent * 0.8f : ColBtnHov) : sBg, ColText);
        ry += BH + BGap + 20;

        var back = new Rectangle(bx, ry, BW, BH);
        DrawButton(sb, back, "Back", back.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
    }

    private void HandleRenderQuality(int mx, int my, int sw, int sh,
                                     bool clicked, bool held, bool released)
    {
        var (wx, wy, _) = RenderQualityLayout(sw, sh);
        int bx  = wx + (PW - BW) / 2;
        int ry0 = wy + TPad + TH;
        int ryS = ry0 + RowH;

        var rdTrack = SliderTrack(wx, ry0);
        var rdHit   = new Rectangle(rdTrack.X, ry0 + 4, rdTrack.Width, RowH - 8);

        if (clicked && rdHit.Contains(mx, my))  _dragging = DragTarget.RenderDist;
        if (released)                            _dragging = DragTarget.None;

        if (held && _dragging == DragTarget.RenderDist)
        {
            float t = Math.Clamp((mx - rdTrack.X) / (float)rdTrack.Width, 0f, 1f);
            _renderDist = MathF.Round(16f + t * (256f - 16f));
        }

        if (clicked)
        {
            var smoothBtn = new Rectangle(bx, ryS, BW, BH);
            if (smoothBtn.Contains(mx, my)) _smoothLighting = !_smoothLighting;

            int backY = ryS + BH + BGap + 20;
            if (new Rectangle(bx, backY, BW, BH).Contains(mx, my))
            {
                _dragging = DragTarget.None;
                _screen   = Screen.Settings;
            }
        }
    }

    // ══════════════════════════ CONTROLS ══════════════════════════════════════

    private (int wx, int wy, int ph) ControlsLayout(int sw, int sh)
    {
        int ph = TPad + TH + RowH + 20 + BH + TPad;
        return ((sw - PW) / 2, (sh - ph) / 2, ph);
    }

    private void DrawControls(SpriteBatch sb, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph) = ControlsLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "CONTROLS", wx, wy);

        int bx = wx + (PW - BW) / 2;
        int ry = wy + TPad + TH;

        DrawSlider(sb, wx, ry, "Mouse Sensitivity", $"{_sensPct:F0}%", _sensPct / 100f);
        ry += RowH + 20;

        var back = new Rectangle(bx, ry, BW, BH);
        DrawButton(sb, back, "Back", back.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
    }

    private void HandleControls(int mx, int my, int sw, int sh,
                                bool clicked, bool held, bool released)
    {
        var (wx, wy, _) = ControlsLayout(sw, sh);
        int bx  = wx + (PW - BW) / 2;
        int ry0 = wy + TPad + TH;

        var sensTrack = SliderTrack(wx, ry0);
        var sensHit   = new Rectangle(sensTrack.X, ry0 + 4, sensTrack.Width, RowH - 8);

        if (clicked && sensHit.Contains(mx, my)) _dragging = DragTarget.Sensitivity;
        if (released)                             _dragging = DragTarget.None;

        if (held && _dragging == DragTarget.Sensitivity)
        {
            float t = Math.Clamp((mx - sensTrack.X) / (float)sensTrack.Width, 0f, 1f);
            _sensPct = MathF.Round(t * 100f);
        }

        if (clicked)
        {
            int backY = ry0 + RowH + 20;
            if (new Rectangle(bx, backY, BW, BH).Contains(mx, my))
            {
                _dragging = DragTarget.None;
                _screen   = Screen.Settings;
            }
        }
    }

    // ════════════════════════════ STATS ═══════════════════════════════════════

    private const int StatCount = 5;

    private (int wx, int wy, int ph) StatsLayout(int sw, int sh)
    {
        int ph = TPad + TH + StatCount * StH + 22 + BH + TPad;
        return ((sw - PW) / 2, (sh - ph) / 2, ph);
    }

    private void DrawStats(SpriteBatch sb, Player player, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph) = StatsLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "STATISTICS", wx, wy);

        int ry = wy + TPad + TH;
        var ts = TimeSpan.FromSeconds(_playTimeSec);

        DrawStatRow(sb, wx, ry, "Play Time",
            $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}");  ry += StH;
        DrawStatRow(sb, wx, ry, "Blocks Placed",  $"{_blocksPlaced}");    ry += StH;
        DrawStatRow(sb, wx, ry, "Blocks Broken",  $"{_blocksBroken}");    ry += StH;
        DrawStatRow(sb, wx, ry, "Position",
            $"{player.Position.X:F1}  {player.Position.Y:F1}  {player.Position.Z:F1}"); ry += StH;
        DrawStatRow(sb, wx, ry, "Health",
            $"{(int)player.Health} / {(int)Player.MaxHealth}");            ry += StH + 22;

        int bx   = wx + (PW - BW) / 2;
        var back = new Rectangle(bx, ry, BW, BH);
        DrawButton(sb, back, "Back", back.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
    }

    private void HandleStats(int mx, int my, int sw, int sh)
    {
        var (wx, wy, _) = StatsLayout(sw, sh);
        int ry = wy + TPad + TH + StatCount * StH + 22;
        int bx = wx + (PW - BW) / 2;
        if (new Rectangle(bx, ry, BW, BH).Contains(mx, my)) _screen = Screen.Main;
    }

    // ═══════════════════════ Drawing primitives ═══════════════════════════════

    private Texture2D GetFillCorner(int r)
    {
        if (_fillCornerCache.TryGetValue(r, out var t)) return t;
        var tex = new Texture2D(_gd, r, r);
        var px  = new Color[r * r];
        for (int py = 0; py < r; py++)
        for (int px2 = 0; px2 < r; px2++)
        {
            float dx = r - (px2 + 0.5f), dy = r - (py + 0.5f);
            float a  = MathHelper.Clamp(r + 0.5f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
            px[py * r + px2] = new Color((byte)(a * 255f), (byte)(a * 255f), (byte)(a * 255f), (byte)(a * 255f));
        }
        tex.SetData(px);
        return _fillCornerCache[r] = tex;
    }

    private Texture2D GetOutlineCorner(int r)
    {
        if (_outlineCornerCache.TryGetValue(r, out var t)) return t;
        var tex  = new Texture2D(_gd, r, r);
        var px   = new Color[r * r];
        float edge = r - 0.5f;
        for (int py = 0; py < r; py++)
        for (int px2 = 0; px2 < r; px2++)
        {
            float dx   = r - (px2 + 0.5f), dy = r - (py + 0.5f);
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float a    = MathHelper.Clamp(1.0f - MathF.Abs(dist - edge), 0f, 1f);
            px[py * r + px2] = new Color((byte)(a * 255f), (byte)(a * 255f), (byte)(a * 255f), (byte)(a * 255f));
        }
        tex.SetData(px);
        return _outlineCornerCache[r] = tex;
    }

    private void FillRounded(SpriteBatch sb, int x, int y, int w, int h, Color c, int r)
    {
        r = Math.Min(r, Math.Min(w, h) / 2);
        sb.Draw(_pixel, new Rectangle(x + r, y,         w - 2 * r, h),     c);
        if (r > 0 && h > 2 * r)
        {
            sb.Draw(_pixel, new Rectangle(x,         y + r, r, h - 2 * r), c);
            sb.Draw(_pixel, new Rectangle(x + w - r, y + r, r, h - 2 * r), c);
        }
        if (r > 0)
        {
            var ct = GetFillCorner(r);
            sb.Draw(ct, new Rectangle(x,         y,         r, r), null, c, 0, Vector2.Zero, SpriteEffects.None,                                          0);
            sb.Draw(ct, new Rectangle(x + w - r, y,         r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipHorizontally,                              0);
            sb.Draw(ct, new Rectangle(x,         y + h - r, r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipVertically,                                0);
            sb.Draw(ct, new Rectangle(x + w - r, y + h - r, r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0);
        }
    }

    private void OutlineRounded(SpriteBatch sb, int x, int y, int w, int h, Color c, int r)
    {
        r = Math.Min(r, Math.Min(w, h) / 2);
        if (w > 2 * r)
        {
            sb.Draw(_pixel, new Rectangle(x + r, y,         w - 2 * r, 1), c);
            sb.Draw(_pixel, new Rectangle(x + r, y + h - 1, w - 2 * r, 1), c);
        }
        if (h > 2 * r)
        {
            sb.Draw(_pixel, new Rectangle(x,         y + r, 1, h - 2 * r), c);
            sb.Draw(_pixel, new Rectangle(x + w - 1, y + r, 1, h - 2 * r), c);
        }
        if (r > 0)
        {
            var ct = GetOutlineCorner(r);
            sb.Draw(ct, new Rectangle(x,         y,         r, r), null, c, 0, Vector2.Zero, SpriteEffects.None,                                          0);
            sb.Draw(ct, new Rectangle(x + w - r, y,         r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipHorizontally,                              0);
            sb.Draw(ct, new Rectangle(x,         y + h - r, r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipVertically,                                0);
            sb.Draw(ct, new Rectangle(x + w - r, y + h - r, r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0);
        }
    }

    private void DrawPanel(SpriteBatch sb, int x, int y, int w, int h)
    {
        FillRounded(sb, x, y, w, h, ColPanel, 8);
        OutlineRounded(sb, x, y, w, h, Color.White * 0.12f, 8);
    }

    private void DrawButton(SpriteBatch sb, Rectangle r, string label, Color bg, Color textCol)
    {
        FillRounded(sb, r.X, r.Y, r.Width, r.Height, bg, 6);
        var sz = _font.MeasureString(label) * FsBtn;
        var p  = new Vector2(r.X + (r.Width  - sz.X) / 2f,
                             r.Y + (r.Height - sz.Y) / 2f);
        sb.DrawString(_font, label, p + new Vector2(1, 1), Color.Black * 0.4f, 0, Vector2.Zero, FsBtn, SpriteEffects.None, 0);
        sb.DrawString(_font, label, p,                     textCol,             0, Vector2.Zero, FsBtn, SpriteEffects.None, 0);
    }

    private void DrawTitle(SpriteBatch sb, string title, int wx, int wy)
    {
        var sz  = _font.MeasureString(title) * FsTitle;
        var pos = new Vector2(wx + (PW - sz.X) / 2f, wy + TPad);
        sb.DrawString(_font, title, pos + new Vector2(2, 2), Color.Black * 0.4f, 0, Vector2.Zero, FsTitle, SpriteEffects.None, 0);
        sb.DrawString(_font, title, pos,                     ColText,             0, Vector2.Zero, FsTitle, SpriteEffects.None, 0);
        int sepY = (int)(pos.Y + sz.Y + 8);
        sb.Draw(_pixel, new Rectangle(wx + 18, sepY, PW - 36, 1), ColSep);
    }

    private void DrawSubHeading(SpriteBatch sb, int wx, int ry, string text)
    {
        sb.Draw(_pixel, new Rectangle(wx + 16, ry + 14, PW - 32, 1), ColSep);
        var sz  = _font.MeasureString(text) * FsSub;
        var pos = new Vector2(wx + (PW - sz.X) / 2f, ry + 4);
        sb.DrawString(_font, text, pos, ColMuted, 0, Vector2.Zero, FsSub, SpriteEffects.None, 0);
    }

    private static Rectangle SliderTrack(int wx, int ry) =>
        new Rectangle(wx + 22, ry + 36, PW - 44, 6);

    private void DrawSlider(SpriteBatch sb, int wx, int ry, string label, string value, float t)
    {
        sb.Draw(_pixel, new Rectangle(wx + 16, ry, PW - 32, 1), ColSep * 0.5f);

        sb.DrawString(_font, label,
            new Vector2(wx + 22, ry + 10), ColText, 0, Vector2.Zero, FsLabel, SpriteEffects.None, 0);

        var vsz = _font.MeasureString(value) * FsValue;
        sb.DrawString(_font, value,
            new Vector2(wx + PW - 22 - vsz.X, ry + 10), ColMuted, 0, Vector2.Zero, FsValue, SpriteEffects.None, 0);

        var track = SliderTrack(wx, ry);
        FillRounded(sb, track.X, track.Y, track.Width, track.Height, ColDark, 3);

        int fillW = Math.Max((int)(track.Width * t), 6);
        FillRounded(sb, track.X, track.Y, fillW, track.Height, ColAccent * 0.85f, 3);

        const int HR = 7;
        int hx = track.X + (int)(track.Width * t);
        int hy = track.Y + track.Height / 2;
        FillRounded(sb, hx - HR, hy - HR, HR * 2, HR * 2, ColText, HR);
    }

    private void DrawStatRow(SpriteBatch sb, int wx, int ry, string label, string value)
    {
        sb.Draw(_pixel, new Rectangle(wx + 16, ry, PW - 32, 1), ColSep * 0.4f);
        sb.DrawString(_font, label,
            new Vector2(wx + 22, ry + 6), ColMuted, 0, Vector2.Zero, FsStat, SpriteEffects.None, 0);
        var vsz = _font.MeasureString(value) * FsStat;
        sb.DrawString(_font, value,
            new Vector2(wx + PW - 22 - vsz.X, ry + 6), ColText, 0, Vector2.Zero, FsStat, SpriteEffects.None, 0);
    }
}
