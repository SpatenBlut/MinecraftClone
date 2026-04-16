using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Gameplay;

namespace MinecraftClone.UI;

public class PauseMenu
{
    private enum Screen { Main, Settings, Stats }
    private Screen _screen;

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
    public float MouseSensitivity { get; set; } = 0.0006f;
    public float Fov              { get; set; } = 75f;

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
        _screen = Screen.Main;
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

        if (ms.LeftButton != ButtonState.Pressed || lastMs.LeftButton != ButtonState.Released) return;

        int mx = ms.X, my = ms.Y;
        int sw = _gd.Viewport.Width, sh = _gd.Viewport.Height;

        switch (_screen)
        {
            case Screen.Main:     HandleMain(mx, my, sw, sh);     break;
            case Screen.Settings: HandleSettings(mx, my, sw, sh); break;
            case Screen.Stats:    HandleStats(mx, my, sw, sh);    break;
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
        ("Resume",     false, true ),
        ("Settings",   false, false),
        ("Statistics", false, false),
        ("Main Menu",  false, false),
        ("Quit Game",  true,  false),
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
        if      (btns[0].Contains(mx, my)) WantsResume   = true;
        else if (btns[1].Contains(mx, my)) _screen       = Screen.Settings;
        else if (btns[2].Contains(mx, my)) _screen       = Screen.Stats;
        else if (btns[3].Contains(mx, my)) WantsMainMenu = true;
        else if (btns[4].Contains(mx, my)) WantsQuit     = true;
    }

    // ══════════════════════════ SETTINGS ══════════════════════════════════════

    private (int wx, int wy, int ph) SettingsLayout(int sw, int sh)
    {
        int ph = TPad + TH + 2 * RowH + 20 + BH + TPad;
        return ((sw - PW) / 2, (sh - ph) / 2, ph);
    }

    private (Rectangle dec, Rectangle inc) SmBtns(int wx, int ry)
    {
        int incX = wx + PW - 24 - SBW;
        int decX = incX - SBW - 8;
        int by   = ry + (RowH - SBH) / 2;
        return (new Rectangle(decX, by, SBW, SBH),
                new Rectangle(incX, by, SBW, SBH));
    }

    private void DrawSettings(SpriteBatch sb, int sw, int sh, int mx, int my)
    {
        var (wx, wy, ph) = SettingsLayout(sw, sh);
        DrawPanel(sb, wx, wy, PW, ph);
        DrawTitle(sb, "SETTINGS", wx, wy);

        int ry = wy + TPad + TH;

        var (sD, sI) = SmBtns(wx, ry);
        DrawSettingRow(sb, wx, ry, "Mouse Sensitivity",
            $"{MouseSensitivity * 10000f:F0}", sD, sI, mx, my);
        ry += RowH;

        var (fD, fI) = SmBtns(wx, ry);
        DrawSettingRow(sb, wx, ry, "Field of View",
            $"{Fov:F0}°", fD, fI, mx, my);
        ry += RowH + 20;

        int bx   = wx + (PW - BW) / 2;
        var back = new Rectangle(bx, ry, BW, BH);
        DrawButton(sb, back, "Back", back.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
    }

    private void HandleSettings(int mx, int my, int sw, int sh)
    {
        var (wx, wy, _) = SettingsLayout(sw, sh);
        int ry = wy + TPad + TH;

        var (sD, sI) = SmBtns(wx, ry);
        if (sD.Contains(mx, my)) MouseSensitivity = MathF.Round(Math.Max(0.0001f, MouseSensitivity - 0.0001f), 4);
        if (sI.Contains(mx, my)) MouseSensitivity = MathF.Round(Math.Min(0.0030f, MouseSensitivity + 0.0001f), 4);
        ry += RowH;

        var (fD, fI) = SmBtns(wx, ry);
        if (fD.Contains(mx, my)) Fov = Math.Max(40f, Fov - 5f);
        if (fI.Contains(mx, my)) Fov = Math.Min(120f, Fov + 5f);
        ry += RowH + 20;

        int bx = wx + (PW - BW) / 2;
        if (new Rectangle(bx, ry, BW, BH).Contains(mx, my)) _screen = Screen.Main;
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

    private void DrawSettingRow(SpriteBatch sb, int wx, int ry,
                                string label, string value,
                                Rectangle dec, Rectangle inc, int mx, int my)
    {
        sb.Draw(_pixel, new Rectangle(wx + 16, ry, PW - 32, 1), ColSep * 0.5f);

        var lsz = _font.MeasureString(label) * FsLabel;
        sb.DrawString(_font, label,
            new Vector2(wx + 22, ry + (RowH - lsz.Y) / 2f),
            ColText, 0, Vector2.Zero, FsLabel, SpriteEffects.None, 0);

        var vsz = _font.MeasureString(value) * FsValue;
        sb.DrawString(_font, value,
            new Vector2(dec.X - vsz.X - 14, ry + (RowH - vsz.Y) / 2f),
            ColMuted, 0, Vector2.Zero, FsValue, SpriteEffects.None, 0);

        DrawButton(sb, dec, "−", dec.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
        DrawButton(sb, inc, "+", inc.Contains(mx, my) ? ColBtnHov : ColBtn, ColText);
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
