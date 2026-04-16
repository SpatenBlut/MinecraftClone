using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Gameplay;
using MinecraftClone.World;

namespace MinecraftClone.UI;

public class HUD
{
    private SpriteFont         _font;
    private Texture2D          _pixelTexture;
    private Texture2D          _blockAtlas;
    private GraphicsDevice     _graphicsDevice;
    private BlockIconRenderer  _iconRenderer;

    private float _smoothFps = 0f;

    // Pixel-Art Herz-Muster (8×6)
    private static readonly bool[,] HeartPattern =
    {
        { false, true,  true,  false, false, true,  true,  false },
        { true,  true,  true,  true,  true,  true,  true,  true  },
        { true,  true,  true,  true,  true,  true,  true,  true  },
        { false, true,  true,  true,  true,  true,  true,  false },
        { false, false, true,  true,  true,  true,  false, false },
        { false, false, false, true,  true,  false, false, false },
    };
    private const float HeartPixelSize = 2.2f;
    private const int   HeartCols      = 8;
    private const int   HeartRows      = 6;
    private static readonly int HeartW = (int)(HeartCols * HeartPixelSize);
    private static readonly int HeartH = (int)(HeartRows * HeartPixelSize);
    private const int TotalHearts      = 10;

    // ── Dark Steel Farbpalette ────────────────────────────────────────────────
    private static readonly Color DsBg    = new Color( 30,  34,  42); // #1E222A Hintergrund
    private static readonly Color DsSlot  = new Color( 45,  50,  60); // #2D323C Slot-Fill
    private static readonly Color DsLight = new Color( 90, 100, 120); // #5A6478 Bevel-Highlight
    private static readonly Color DsDark  = new Color( 15,  17,  22); // #0F1116 Bevel-Schatten
    private static readonly Color DsText  = new Color(200, 210, 220); // #C8D2DC Label-Text
    private static readonly Color DsSep   = new Color( 60,  70,  85); // #3C4655 Trennlinie
    private static readonly Color DsIcon  = new Color(120, 140, 165); // Rüstungs-Icons
    private static readonly Color DsSel   = new Color(150, 200, 255); // #96C8FF Auswahl
    private static readonly Color DsArrow = new Color( 80,  95, 115); // #505F73 Pfeil

    public HUD(GraphicsDevice graphicsDevice, SpriteFont font, Texture2D blockAtlas)
    {
        _graphicsDevice = graphicsDevice;
        _font           = font;
        _blockAtlas     = blockAtlas;
        _pixelTexture   = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // 3D-Block-Icons vorrendern (einmal beim Start)
        _iconRenderer = new BlockIconRenderer(graphicsDevice, blockAtlas);
        _iconRenderer.Initialize();
    }

    // ─── Haupt-Draw ──────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, Player player, Inventory inventory,
        int screenWidth, int screenHeight, GameTime gameTime,
        bool inventoryOpen = false, int mouseX = 0, int mouseY = 0)
    {
        float fps = gameTime.ElapsedGameTime.TotalSeconds > 0
            ? (float)(1.0 / gameTime.ElapsedGameTime.TotalSeconds) : _smoothFps;
        _smoothFps = _smoothFps * 0.85f + fps * 0.15f;

        sb.Begin();

        if (!inventoryOpen)
            DrawCrosshair(sb, screenWidth, screenHeight);

        DrawHotbar(sb, inventory, screenWidth, screenHeight, out int hotbarY, out int hotbarStartX);
        DrawHearts(sb, player, hotbarStartX, hotbarY);
        DrawFps(sb, screenWidth);
        DrawDebugInfo(sb, player, 10, 10);

        if (inventoryOpen)
            DrawInventory(sb, inventory, screenWidth, screenHeight, mouseX, mouseY);

        sb.End();
    }

    // ─── Crosshair ───────────────────────────────────────────────────────────

    private void DrawCrosshair(SpriteBatch sb, int sw, int sh)
    {
        int cx = sw / 2, cy = sh / 2;
        sb.Draw(_pixelTexture, new Rectangle(cx - 10, cy - 1, 20, 2), Color.White);
        sb.Draw(_pixelTexture, new Rectangle(cx - 1, cy - 10, 2, 20), Color.White);
    }

    // ─── Hotbar ──────────────────────────────────────────────────────────────

    private void DrawHotbar(SpriteBatch sb, Inventory inventory,
        int sw, int sh, out int hotbarY, out int hotbarStartX)
    {
        const int slotSize = 46, spacing = 3, slots = 9;
        int totalWidth = (slotSize + spacing) * slots - spacing;
        int startX = (sw - totalWidth) / 2;
        hotbarY      = sh - slotSize - 10;
        hotbarStartX = startX;

        for (int i = 0; i < slots; i++)
        {
            int x = startX + (slotSize + spacing) * i;
            Color c = i == inventory.SelectedSlot ? Color.White : Color.Gray;
            FillRounded(sb, x, hotbarY, slotSize, slotSize, c * 0.45f, 6);
            OutlineRounded(sb, x, hotbarY, slotSize, slotSize, c, 6);
            var item = inventory.GetSlot(i);
            if (!item.IsEmpty)
                DrawIcon(sb, item, new Rectangle(x, hotbarY, slotSize, slotSize));
        }
    }

    // ─── Herzen ──────────────────────────────────────────────────────────────

    private void DrawHearts(SpriteBatch sb, Player player, int hotbarStartX, int hotbarY)
    {
        float heartSpacing = (4 * (46 + 3) - 3 - TotalHearts * HeartW) / (float)(TotalHearts - 1);
        int startX = hotbarStartX;
        int y      = hotbarY - HeartH - 6;
        float hp   = player.Health;
        for (int i = 0; i < TotalHearts; i++)
        {
            int x      = startX + (int)(i * (HeartW + heartSpacing));
            float hHp  = hp - i * 2f;
            DrawHeartPixels(sb, x, y, true,  new Color(30, 0, 0));
            if      (hHp >= 2f) DrawHeartPixels(sb, x, y, true,  Color.Red);
            else if (hHp >= 1f) DrawHeartPixels(sb, x, y, false, Color.Red);
        }
    }

    private void DrawHeartPixels(SpriteBatch sb, int x, int y, bool full, Color color)
    {
        int colLimit = full ? HeartCols : HeartCols / 2;
        for (int row = 0; row < HeartRows; row++)
        for (int col = 0; col < colLimit; col++)
        {
            if (!HeartPattern[row, col]) continue;
            sb.Draw(_pixelTexture,
                new Rectangle(x + (int)(col * HeartPixelSize), y + (int)(row * HeartPixelSize),
                    (int)Math.Ceiling(HeartPixelSize), (int)Math.Ceiling(HeartPixelSize)), color);
        }
    }

    // ─── FPS / Debug ─────────────────────────────────────────────────────────

    private void DrawFps(SpriteBatch sb, int sw)
    {
        string t = $"FPS: {(int)_smoothFps}";
        Vector2 sz = _font.MeasureString(t) * 0.33f;
        sb.DrawString(_font, t, new Vector2(sw - sz.X - 10, 10),
            Color.White, 0f, Vector2.Zero, 0.33f, SpriteEffects.None, 0f);
    }

    private void DrawDebugInfo(SpriteBatch sb, Player player, int x, int y)
    {
        sb.DrawString(_font, $"Pos: {player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1}",
            new Vector2(x, y), Color.White, 0f, Vector2.Zero, 0.31f, SpriteEffects.None, 0f);
        sb.DrawString(_font, $"Vel: {player.Velocity.X:F1}, {player.Velocity.Y:F1}, {player.Velocity.Z:F1}",
            new Vector2(x, y + 20), Color.White, 0f, Vector2.Zero, 0.31f, SpriteEffects.None, 0f);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DARK STEEL INVENTAR
    // Fenster: 352×177 GUI-Pixel
    // Layout: Rüstung (links) | Mittelgap | 9er-Grid | Gap | Crafting (rechts)
    // ═══════════════════════════════════════════════════════════════════════════

    // GUI-Scale: max(1, min(sw/320, sh/240) - 2)
    private static int Sc(int sw, int sh) => Math.Max(2, Math.Min(sw / 320, sh / 240) - 1);

    // GUI-Pixel → Screen-Rectangle (relativ zum Fenster-Ursprung wx/wy)
    private static Rectangle R(int wx, int wy, int gx, int gy, int gw, int gh, int sc) =>
        new Rectangle(wx + gx * sc, wy + gy * sc, gw * sc, gh * sc);

    // ── Erhobenes Panel (Dark Steel) ─────────────────────────────────────────
    private void DsPanel(SpriteBatch sb, int x, int y, int w, int h, int sc)
    {
        FillRounded(sb, x, y, w, h, DsBg, 8);
        OutlineRounded(sb, x, y, w, h, Color.White * 0.15f, 8);
    }

    // ── Slot ─────────────────────────────────────────────────────────────────
    private void DsSlotDraw(SpriteBatch sb, int x, int y, int w, int h, bool hovered, int sc)
    {
        FillRounded(sb, x, y, w, h, DsSlot, 4);
        OutlineRounded(sb, x, y, w, h, Color.White * 0.15f, 4);
        if (hovered)
            FillRounded(sb, x, y, w, h, Color.White * 0.25f, 4);
    }

    // ── Slot über GUI-Koordinaten ─────────────────────────────────────────────
    private void SlotG(SpriteBatch sb, int wx, int wy, int gx, int gy, int sc, bool hovered) =>
        DsSlotDraw(sb, wx + gx * sc, wy + gy * sc, 18 * sc, 18 * sc, hovered, sc);

    // ── Label mit Schatten (Dark Steel) ──────────────────────────────────────
    private void DsLabel(SpriteBatch sb, string text, int wx, int wy, int gx, int gy, int sc)
    {
        float s = sc * 0.18f;
        var pos = new Vector2(wx + gx * sc, wy + gy * sc);
        sb.DrawString(_font, text, pos + new Vector2(sc * 0.5f, sc * 0.5f),
            DsDark, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, DsText, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
    }

    // ── Haupt-DrawInventory ───────────────────────────────────────────────────
    // Layout (286 × 94 GUI-Pixel):
    //   Armor  gx=6        | Inventar gx=28+col*19  | Crafting gx=202/221
    //   gy=8,27,46,65      | gy=8/27/46 (3 Reihen)  | gy=27/46 (Reihen 2+3)
    //   Separator gy=67 | Hotbar gy=69
    private void DrawInventory(SpriteBatch sb, Inventory inv, int sw, int sh, int mx, int my)
    {
        int sc = Sc(sw, sh);
        const int GuiW = 286, GuiH = 94;
        int winW = GuiW * sc, winH = GuiH * sc;
        int wx = (sw - winW) / 2, wy = (sh - winH) / 2;

        // Hintergrund
        FillRounded(sb, wx, wy, winW, winH, DsBg, 8);
        OutlineRounded(sb, wx, wy, winW, winH, Color.White * 0.15f, 8);

        // ── Rüstungs-Slots (36–39), direkt links vom Grid ────────────────────
        int[] armorGy = { 8, 27, 46, 65 };
        for (int i = 0; i < 4; i++)
        {
            bool hov = R(wx, wy, 6, armorGy[i], 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, 6, armorGy[i], sc, hov);
            DrawArmorIcon(sb, wx, wy, 6, armorGy[i], sc, i);
        }

        // ── Haupt-Inventar (Slots 9–35, 3 Reihen × 9 Spalten) ───────────────
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 9; col++)
        {
            int idx = 9 + row * 9 + col;
            int gx  = 28 + col * 19, gy = 8 + row * 19;
            bool hov = R(wx, wy, gx, gy, 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, gx, gy, sc, hov);
            var item = inv.GetSlot(idx);
            if (!item.IsEmpty) DrawIconMc(sb, item, R(wx, wy, gx, gy, 18, 18, sc), sc);
        }

        // ── Trennlinie ────────────────────────────────────────────────────────
        sb.Draw(_pixelTexture, R(wx, wy, 28, 67, 170, 1, sc), DsSep);

        // ── Hotbar-Zeile (Slots 0–8, gy=69) ──────────────────────────────────
        for (int col = 0; col < 9; col++)
        {
            int gx = 28 + col * 19;
            bool hov = R(wx, wy, gx, 69, 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, gx, 69, sc, hov);
            var item = inv.GetSlot(col);
            if (!item.IsEmpty) DrawIconMc(sb, item, R(wx, wy, gx, 69, 18, 18, sc), sc);
        }

        // ── Crafting-Label + 2×2 Grid (direkt rechts, Reihen 2+3) ───────────
        DsLabel(sb, "Crafting", wx, wy, 202, 8, sc);
        int[] cGx = { 202, 221, 202, 221 };
        int[] cGy = {  27,  27,  46,  46 };
        for (int i = 0; i < 4; i++)
        {
            bool hov = R(wx, wy, cGx[i], cGy[i], 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, cGx[i], cGy[i], sc, hov);
        }

        // Crafting-Pfeil
        DrawCraftArrow(sb, wx, wy, sc);

        // Ergebnis-Slot (vertikal zentriert mit 2×2-Grid)
        {
            bool hov = R(wx, wy, 262, 36, 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, 262, 36, sc, hov);
        }

        // ── Cursor-Stack ──────────────────────────────────────────────────────
        var cursor = inv.CursorStack;
        if (!cursor.IsEmpty)
        {
            int sz = 16 * sc;
            DrawIconMc(sb, cursor, new Rectangle(mx - sz / 2, my - sz / 2, sz, sz), sc, drawCount: true);
        }
    }

    // ── Slot-Hit-Test ─────────────────────────────────────────────────────────
    public int GetInventorySlotAt(int mx, int my, int sw, int sh)
    {
        int sc = Sc(sw, sh);
        int wx = (sw - 286 * sc) / 2;
        int wy = (sh -  94 * sc) / 2;

        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 9; col++)
            if (R(wx, wy, 28 + col * 19, 8 + row * 19, 18, 18, sc).Contains(mx, my))
                return 9 + row * 9 + col;

        for (int col = 0; col < 9; col++)
            if (R(wx, wy, 28 + col * 19, 69, 18, 18, sc).Contains(mx, my))
                return col;

        return -1;
    }

    // ── Rüstungs-Slot-Icons (10×10 GUI-Pixel, Pixel-Art) ─────────────────────
    private void DrawArmorIcon(SpriteBatch sb, int wx, int wy, int gx, int gy, int sc, int idx)
    {
        int ps = sc;
        int ox = wx + (gx + 4) * sc;
        int oy = wy + (gy + 4) * sc;
        Color ic = DsIcon;

        switch (idx)
        {
            case 0: // Helm: Kuppel mit Krempe
                sb.Draw(_pixelTexture, new Rectangle(ox + 2*ps, oy,        6*ps,  ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox +   ps, oy +   ps, 8*ps, 2*ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox +   ps, oy + 3*ps, 2*ps,   ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox + 7*ps, oy + 3*ps, 2*ps,   ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox,        oy + 4*ps,10*ps,   ps), ic);
                break;

            case 1: // Brustplatte: Schultern + Torso
                sb.Draw(_pixelTexture, new Rectangle(ox,        oy,        3*ps,   ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox + 7*ps, oy,        3*ps,   ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox,        oy +   ps,10*ps, 5*ps), ic);
                break;

            case 2: // Beinling: Hüftband + zwei Beine
                sb.Draw(_pixelTexture, new Rectangle(ox,        oy,       10*ps, 2*ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox,        oy + 2*ps, 4*ps, 7*ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox + 6*ps, oy + 2*ps, 4*ps, 7*ps), ic);
                break;

            case 3: // Schuhe: zwei Schäfte + Sohlen
                sb.Draw(_pixelTexture, new Rectangle(ox,        oy,        4*ps, 6*ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox + 6*ps, oy,        4*ps, 6*ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox,        oy + 6*ps, 5*ps, 2*ps), ic);
                sb.Draw(_pixelTexture, new Rectangle(ox + 5*ps, oy + 6*ps, 5*ps, 2*ps), ic);
                break;
        }
    }

    // ── Crafting-Pfeil (→), zentriert auf Crafting-Grid-Mitte y=45 ───────────
    private void DrawCraftArrow(SpriteBatch sb, int wx, int wy, int sc)
    {
        // Körper
        sb.Draw(_pixelTexture, R(wx, wy, 242, 44, 13, 3, sc), DsArrow);
        // Pfeilspitze (3 Schichten)
        sb.Draw(_pixelTexture, R(wx, wy, 255, 43,  2, 5, sc), DsArrow);
        sb.Draw(_pixelTexture, R(wx, wy, 257, 44,  2, 3, sc), DsArrow);
        sb.Draw(_pixelTexture, R(wx, wy, 259, 45,  1, 1, sc), DsArrow);
    }

    // ── Item-Icon (3D-vorgerendert, in 18×18 Slot) ───────────────────────────
    private void DrawIconMc(SpriteBatch sb, ItemStack item, Rectangle slotRect, int sc,
        bool drawCount = true)
    {
        if (item.IsEmpty) return;

        int pad  = sc * 3;
        var dest = new Rectangle(slotRect.X + pad, slotRect.Y + pad,
                                 slotRect.Width - 2 * pad, slotRect.Height - 2 * pad);

        sb.Draw(_iconRenderer.GetIcon(item.Block), dest, Color.White);

        if (drawCount && item.Count > 1)
        {
            string s     = item.Count.ToString();
            float  fsc   = sc * 0.17f;
            Vector2 size = _font.MeasureString(s) * fsc;
            sb.DrawString(_font, s,
                new Vector2(slotRect.Right - size.X - sc + sc * 0.5f,
                            slotRect.Bottom - size.Y - sc + sc * 0.5f),
                DsDark * 0.7f, 0f, Vector2.Zero, fsc, SpriteEffects.None, 0f);
            sb.DrawString(_font, s,
                new Vector2(slotRect.Right - size.X - sc, slotRect.Bottom - size.Y - sc),
                Color.White, 0f, Vector2.Zero, fsc, SpriteEffects.None, 0f);
        }
    }

    // ── Item-Icon für Hotbar (größere Slots, 3D) ──────────────────────────────
    private void DrawIcon(SpriteBatch sb, ItemStack item, Rectangle slotRect, bool drawCount = true)
    {
        if (item.IsEmpty) return;

        int pad = slotRect.Width / 8;
        sb.Draw(_iconRenderer.GetIcon(item.Block),
            new Rectangle(slotRect.X + pad, slotRect.Y + pad,
                          slotRect.Width - pad * 2, slotRect.Height - pad * 2),
            Color.White);

        if (drawCount && item.Count > 1)
        {
            string s     = item.Count.ToString();
            float  fsc   = 0.33f;
            Vector2 size = _font.MeasureString(s) * fsc;
            sb.DrawString(_font, s,
                new Vector2(slotRect.Right - size.X - 2, slotRect.Bottom - size.Y - 2),
                Color.White, 0f, Vector2.Zero, fsc, SpriteEffects.None, 0f);
        }
    }

    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    private void DrawOutline(SpriteBatch sb, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(_pixelTexture, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    // ── Ecken-Textur-Cache (anti-aliased, einmal gebaked) ────────────────────
    private readonly Dictionary<int, Texture2D> _fillCornerCache    = new();
    private readonly Dictionary<int, Texture2D> _outlineCornerCache = new();

    private Texture2D GetFillCorner(int r)
    {
        if (_fillCornerCache.TryGetValue(r, out var t)) return t;
        var tex = new Texture2D(_graphicsDevice, r, r);
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
        var tex  = new Texture2D(_graphicsDevice, r, r);
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
        // Mittelteil (3 Rechtecke, keine Überlappung mit Ecken)
        sb.Draw(_pixelTexture, new Rectangle(x + r, y,         w - 2 * r, h),     c);
        if (r > 0 && h > 2 * r)
        {
            sb.Draw(_pixelTexture, new Rectangle(x,         y + r, r, h - 2 * r), c);
            sb.Draw(_pixelTexture, new Rectangle(x + w - r, y + r, r, h - 2 * r), c);
        }
        // 4 anti-aliased Ecken
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
        // Gerade Kanten
        if (w > 2 * r)
        {
            sb.Draw(_pixelTexture, new Rectangle(x + r, y,         w - 2 * r, 1), c);
            sb.Draw(_pixelTexture, new Rectangle(x + r, y + h - 1, w - 2 * r, 1), c);
        }
        if (h > 2 * r)
        {
            sb.Draw(_pixelTexture, new Rectangle(x,         y + r, 1, h - 2 * r), c);
            sb.Draw(_pixelTexture, new Rectangle(x + w - 1, y + r, 1, h - 2 * r), c);
        }
        // Anti-aliased Kreisbogen-Ecken
        if (r > 0)
        {
            var ct = GetOutlineCorner(r);
            sb.Draw(ct, new Rectangle(x,         y,         r, r), null, c, 0, Vector2.Zero, SpriteEffects.None,                                          0);
            sb.Draw(ct, new Rectangle(x + w - r, y,         r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipHorizontally,                              0);
            sb.Draw(ct, new Rectangle(x,         y + h - r, r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipVertically,                                0);
            sb.Draw(ct, new Rectangle(x + w - r, y + h - r, r, r), null, c, 0, Vector2.Zero, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, 0);
        }
    }
}
