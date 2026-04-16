using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Gameplay;

namespace MinecraftClone.UI;

public class PauseMenu
{
    private enum Screen     { Main, Settings, Stats }
    private enum DragTarget { None, Sensitivity, Fov }

    private Screen     _screen;
    private DragTarget _dragging = DragTarget.None;

    private readonly SpriteFont     _font;
    private readonly Texture2D      _pixel;
    private readonly GraphicsDevice _gd;

    private readonly Dictionary<int, Texture2D> _fillCornerCache    = new();
    private readonly Dictionary<int, Texture2D> _outlineCornerCache = new();

    // ── Dark‑Steel palette ────────────────────────────────────────────────────
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

    // ── Settings (Game1 reads and applies these) ──────────────────────────────
    // Sensitivity is stored internally as Minecraft % (0-200) and converted on get/set.
    // Minecraft formula: s = pct/200, f = (s*0.6+0.2)^3 * 8, rad/px = f*0.15*(π/180)
    private float _sensPct = 50f;   // Minecraft default (0-100 range, 50 = internal 0.5)

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

    public float Fov { get; set; } = 70f;

    // ── Single-frame intent flags ─────────────────────────────────────────────
    public bool WantsResume   { get; private set; }
    public bool WantsMainMenu { get; private set; }
    public bool WantsQuit     { get; private set; }

    private int   _blocksPlaced;
    private int   _blocksBroken;
    private float _playTimeSec;

    // ── Layout constants ──────────────────────────────────────────────────────
    private const int PW   = 440;   // panel width
    private const int BW   = 362;   // button width
    private const int BH   = 54;    // button height
    private const int BGap = 10;    // gap between buttons
    private const int TPad = 28;    // top/bottom padding inside panel
    private const int TH   = 54;    // title area height
    private const int RowH = 62;    // settings row height
    private const int SBW  = 40;    // small +/- button width
    private const int SBH  = 36;    // small +/- button height
    private const int StH  = 36;    // stats row height

    // ── Font scales ───────────────────────────────────────────────────────────
    private const float FsTitle = 0.78f;
    private const float FsBtn   = 0.52f;
    private const float FsLabel = 0.42f;
    private const float FsValue = 0.40f;
    private const float FsStat  = 0.38f;

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

        // ESC: navigate back or resume
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
            case Screen.Main:     if (clicked) HandleMain(mx, my, sw, sh);                  break;
            case Screen.Settings: HandleSettings(mx, my, sw, sh, clicked, held, released);  break;
            case Screen.Stats:    if (clicked) HandleStats(mx, my, sw, sh);                  break;
        }
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, Player player, int sw, int sh, int mx, int my)
    {
        sb.Draw(_pixel, new Rectangle(0, 0, sw, sh), ColOverlay);
        switch (_screen)
        {
            case Screen.Main:     DrawMain(sb, sw, sh, mx, my);          break;
            case Screen.Settings: DrawSettings(sb, sw, sh, mx, my);      break;
            case Screen.Stats:    DrawStats(sb, player, sw, sh, mx, my); break;
        }
    }

    // ═══════════════════════════════ MAIN ════════════════════════════════════

    private static readonly (string Label, bool Danger, bool Accent)[] MainItems =
    {
        ("Main Menu",  false, false),
        ("Settings",   false, false),
        ("Statistics", false, false),
        ("Leave",      true,  false),
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
            var (label, danger, accent) = MainItems[i];
            bool hov  = btns[i].Contains(mx, my);
            Color bg   = danger ? (hov ? ColBtnRedH : ColBtnRed) : hov ? ColBtnHov : ColBtn;
            Color text = accent ? ColAccent : ColText;
            DrawButton(sb, btns[i], label, bg, text);
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

    // ══════════════════════════ SETTINGS ══════════════════════════════════════

    private (int wx, int wy, int ph) SettingsLayout(int sw, int sh)
    {
        int ph = TPad + TH + 2 * RowH + 20 + BH + TPad;
        return ((sw - PW) / 2, (sh - ph) / 2, ph);
    }

    // Track rectangle for a slider row starting at (wx, ry)
    private static Rectangle SliderTrack(int wx, int ry) =>
        new Rectangle(wx + 22, ry + 36, PW - 44, 6);

    private void DrawSettings(SpriteBatch sb, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph) = SettingsLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "SETTINGS", wx, wy);

        int ry = wy + TPad + TH;

        DrawSlider(sb, wx, ry, "Mouse Sensitivity", $"{_sensPct:F0}%", _sensPct / 100f);
        ry += RowH;

        DrawSlider(sb, wx, ry, "Field of View", $"{Fov:F0}", (Fov - 30f) / 80f);
        ry += RowH + 20;

        int bx   = wx + (PW - BW) / 2;
        var back = new Rectangle(bx, ry, BW, BH);
        DrawButton(sb, back, "Back", back.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
    }

    private void HandleSettings(int mx, int my, int sw, int sh,
                                bool clicked, bool held, bool released)
    {
        var (wx, wy, _) = SettingsLayout(sw, sh);
        int ry0 = wy + TPad + TH;
        int ry1 = ry0 + RowH;

        var sensTrack = SliderTrack(wx, ry0);
        var fovTrack  = SliderTrack(wx, ry1);

        // Expand hit area vertically so the full row is draggable
        var sensHit = new Rectangle(sensTrack.X, ry0 + 4, sensTrack.Width, RowH - 8);
        var fovHit  = new Rectangle(fovTrack.X,  ry1 + 4, fovTrack.Width,  RowH - 8);

        if (clicked)
        {
            if      (sensHit.Contains(mx, my)) _dragging = DragTarget.Sensitivity;
            else if (fovHit .Contains(mx, my)) _dragging = DragTarget.Fov;
        }

        if (released)
            _dragging = DragTarget.None;

        if (held)
        {
            if (_dragging == DragTarget.Sensitivity)
            {
                float t = Math.Clamp((mx - sensTrack.X) / (float)sensTrack.Width, 0f, 1f);
                _sensPct = MathF.Round(t * 100f);
            }
            else if (_dragging == DragTarget.Fov)
            {
                float t = Math.Clamp((mx - fovTrack.X) / (float)fovTrack.Width, 0f, 1f);
                Fov = MathF.Round(30f + t * 80f);
            }
        }

        // Back button — click only
        if (clicked)
        {
            int bx   = wx + (PW - BW) / 2;
            int backY = ry1 + RowH + 20;
            if (new Rectangle(bx, backY, BW, BH).Contains(mx, my))
            {
                _dragging = DragTarget.None;
                _screen   = Screen.Main;
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
        sb.DrawString(_font, label, p + new Vector2(1, 1), Color.Black * 0.4f,
            0, Vector2.Zero, FsBtn, SpriteEffects.None, 0);
        sb.DrawString(_font, label, p, textCol,
            0, Vector2.Zero, FsBtn, SpriteEffects.None, 0);
    }

    private void DrawTitle(SpriteBatch sb, string title, int wx, int wy)
    {
        var sz  = _font.MeasureString(title) * FsTitle;
        var pos = new Vector2(wx + (PW - sz.X) / 2f, wy + TPad);
        sb.DrawString(_font, title, pos + new Vector2(2, 2), Color.Black * 0.4f,
            0, Vector2.Zero, FsTitle, SpriteEffects.None, 0);
        sb.DrawString(_font, title, pos, ColText,
            0, Vector2.Zero, FsTitle, SpriteEffects.None, 0);
        int sepY = (int)(pos.Y + sz.Y + 8);
        sb.Draw(_pixel, new Rectangle(wx + 18, sepY, PW - 36, 1), ColSep);
    }

    private void DrawSlider(SpriteBatch sb, int wx, int ry, string label, string value, float t)
    {
        sb.Draw(_pixel, new Rectangle(wx + 16, ry, PW - 32, 1), ColSep * 0.5f);

        // Label (top-left)
        sb.DrawString(_font, label,
            new Vector2(wx + 22, ry + 10),
            ColText, 0, Vector2.Zero, FsLabel, SpriteEffects.None, 0);

        // Value (top-right)
        var vsz = _font.MeasureString(value) * FsValue;
        sb.DrawString(_font, value,
            new Vector2(wx + PW - 22 - vsz.X, ry + 10),
            ColMuted, 0, Vector2.Zero, FsValue, SpriteEffects.None, 0);

        // Track background
        var track = SliderTrack(wx, ry);
        FillRounded(sb, track.X, track.Y, track.Width, track.Height, ColDark, 3);

        // Filled portion
        int fillW = Math.Max((int)(track.Width * t), 6);
        FillRounded(sb, track.X, track.Y, fillW, track.Height, ColAccent * 0.85f, 3);

        // Handle knob
        const int HR = 7;
        int hx = track.X + (int)(track.Width * t);
        int hy = track.Y + track.Height / 2;
        FillRounded(sb, hx - HR, hy - HR, HR * 2, HR * 2, ColText, HR);
    }

    private void DrawStatRow(SpriteBatch sb, int wx, int ry, string label, string value)
    {
        sb.Draw(_pixel, new Rectangle(wx + 16, ry, PW - 32, 1), ColSep * 0.4f);

        sb.DrawString(_font, label,
            new Vector2(wx + 22, ry + 6),
            ColMuted, 0, Vector2.Zero, FsStat, SpriteEffects.None, 0);

        var vsz = _font.MeasureString(value) * FsStat;
        sb.DrawString(_font, value,
            new Vector2(wx + PW - 22 - vsz.X, ry + 6),
            ColText, 0, Vector2.Zero, FsStat, SpriteEffects.None, 0);
    }
}
