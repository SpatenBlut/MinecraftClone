using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Gameplay;
using MinecraftClone.World;

namespace MinecraftClone.UI;

public class HUD
{
    private SpriteFont    _font;
    private Texture2D     _pixelTexture;
    private Texture2D     _blockAtlas;
    private GraphicsDevice _graphicsDevice;

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
    private const float HeartPixelSize = 2.0f;
    private const int   HeartCols      = 8;
    private const int   HeartRows      = 6;
    private static readonly int HeartW = (int)(HeartCols * HeartPixelSize);
    private static readonly int HeartH = (int)(HeartRows * HeartPixelSize);
    private const int TotalHearts      = 10;

    // ── Minecraft-GUI-Farben (exakt wie Java Edition) ────────────────────────
    private static readonly Color McBg    = new Color(198, 198, 198); // #C6C6C6
    private static readonly Color McSlotC = new Color(139, 139, 139); // #8B8B8B
    private static readonly Color McDark  = new Color(55,  55,  55);  // #373737
    private static readonly Color McLight = Color.White;
    private static readonly Color McText  = new Color(64,  64,  64);  // dunkler Text auf hellem BG

    public HUD(GraphicsDevice graphicsDevice, SpriteFont font, Texture2D blockAtlas)
    {
        _graphicsDevice = graphicsDevice;
        _font           = font;
        _blockAtlas     = blockAtlas;
        _pixelTexture   = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
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
        const int slotSize = 42, spacing = 3, slots = 9;
        int totalWidth = (slotSize + spacing) * slots - spacing;
        int startX = (sw - totalWidth) / 2;
        hotbarY      = sh - slotSize - 10;
        hotbarStartX = startX;

        for (int i = 0; i < slots; i++)
        {
            int x = startX + (slotSize + spacing) * i;
            Color c = i == inventory.SelectedSlot ? Color.White : Color.Gray;
            sb.Draw(_pixelTexture, new Rectangle(x, hotbarY, slotSize, slotSize), c * 0.45f);
            DrawOutline(sb, new Rectangle(x, hotbarY, slotSize, slotSize), c, 2);
            var item = inventory.GetSlot(i);
            if (!item.IsEmpty)
                DrawIcon(sb, item, new Rectangle(x, hotbarY, slotSize, slotSize));
        }
    }

    // ─── Herzen ──────────────────────────────────────────────────────────────

    private void DrawHearts(SpriteBatch sb, Player player, int hotbarStartX, int hotbarY)
    {
        float heartSpacing = (4 * (42 + 3) - 3 - TotalHearts * HeartW) / (float)(TotalHearts - 1);
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
        Vector2 sz = _font.MeasureString(t) * 0.75f;
        sb.DrawString(_font, t, new Vector2(sw - sz.X - 10, 10),
            Color.White, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
    }

    private void DrawDebugInfo(SpriteBatch sb, Player player, int x, int y)
    {
        sb.DrawString(_font, $"Pos: {player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1}",
            new Vector2(x, y), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        sb.DrawString(_font, $"Vel: {player.Velocity.X:F1}, {player.Velocity.Y:F1}, {player.Velocity.Z:F1}",
            new Vector2(x, y + 20), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MINECRAFT SURVIVAL INVENTAR (1:1, Java 1.21)
    // Alle Positionen in GUI-Pixeln; Fenster 176×166 (wie inventory.png)
    // ═══════════════════════════════════════════════════════════════════════════

    // GUI-Scale wie Minecraft: max(2, min(sw/320, sh/240))
    private static int Sc(int sw, int sh) => Math.Max(1, Math.Min(sw / 320, sh / 240) - 2);

    // GUI-Pixel → Screen-Rectangle (relativ zum Fenster-Ursprung wx/wy)
    private static Rectangle R(int wx, int wy, int gx, int gy, int gw, int gh, int sc) =>
        new Rectangle(wx + gx * sc, wy + gy * sc, gw * sc, gh * sc);

    // ── Erhobenes Panel (wie MC-Inventar-Rahmen) ─────────────────────────────
    private void McPanel(SpriteBatch sb, int x, int y, int w, int h, int sc)
    {
        int b = 2 * sc;
        sb.Draw(_pixelTexture, new Rectangle(x, y, w, h), McBg);
        // Links + Rechts (volle Höhe → decken Ecken ab)
        sb.Draw(_pixelTexture, new Rectangle(x,       y, b, h), McLight);
        sb.Draw(_pixelTexture, new Rectangle(x+w-b,   y, b, h), McDark);
        // Oben + Unten (ohne die Ecken → kein Überschreiben)
        sb.Draw(_pixelTexture, new Rectangle(x+b,     y,     w-2*b, b), McLight);
        sb.Draw(_pixelTexture, new Rectangle(x+b,     y+h-b, w-2*b, b), McDark);
    }

    // ── Vertief ter Slot (wie MC-Container-Slot) ─────────────────────────────
    private void McSlot(SpriteBatch sb, int x, int y, int w, int h, bool hovered, int sc)
    {
        int b = sc; // 1 GUI-Pixel Kante
        sb.Draw(_pixelTexture, new Rectangle(x, y, w, h), McSlotC);
        // Links + Rechts (volle Höhe)
        sb.Draw(_pixelTexture, new Rectangle(x,       y, b, h), McDark);
        sb.Draw(_pixelTexture, new Rectangle(x+w-b,   y, b, h), McLight);
        // Oben + Unten (ohne Ecken)
        sb.Draw(_pixelTexture, new Rectangle(x+b,     y,     w-2*b, b), McDark);
        sb.Draw(_pixelTexture, new Rectangle(x+b,     y+h-b, w-2*b, b), McLight);
        if (hovered)
            sb.Draw(_pixelTexture, new Rectangle(x, y, w, h), Color.White * 0.4f);
    }

    // ── Slot über GUI-Koordinaten ────────────────────────────────────────────
    private void SlotG(SpriteBatch sb, int wx, int wy, int gx, int gy, int sc, bool hovered) =>
        McSlot(sb, wx + gx * sc, wy + gy * sc, 18 * sc, 18 * sc, hovered, sc);

    // ── Label mit Schatten (wie MC-Inventar-Beschriftung) ────────────────────
    private void McLabel(SpriteBatch sb, string text, int wx, int wy, int gx, int gy, int sc)
    {
        float s = sc * 0.42f;
        var pos = new Vector2(wx + gx * sc, wy + gy * sc);
        sb.DrawString(_font, text, pos + new Vector2(sc * 0.5f, sc * 0.5f),
            McDark * 0.5f, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, McText, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
    }

    // ── Haupt-DrawInventory ──────────────────────────────────────────────────
    private void DrawInventory(SpriteBatch sb, Inventory inv, int sw, int sh, int mx, int my)
    {
        int sc = Sc(sw, sh);
        const int GuiW = 176, GuiH = 166;
        int winW = GuiW * sc, winH = GuiH * sc;
        int wx = (sw - winW) / 2, wy = (sh - winH) / 2;

        // Dunkles Overlay
        sb.Draw(_pixelTexture, new Rectangle(0, 0, sw, sh), Color.Black * 0.4f);

        // Inventar-Panel
        McPanel(sb, wx, wy, winW, winH, sc);

        // ── Rüstungs-Slots (36–39), links, nur visuell ───────────────────────
        int[] armorGy = { 8, 26, 44, 62 };
        for (int i = 0; i < 4; i++)
        {
            bool hov = R(wx, wy, 8, armorGy[i], 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, 8, armorGy[i], sc, hov);
            DrawArmorIcon(sb, wx, wy, 8, armorGy[i], sc, i);
        }

        // ── Spieler-Silhouette ────────────────────────────────────────────────
        DrawPlayerSilhouette(sb, wx, wy, sc);

        // ── Off-Hand-Slot (x=77, y=62) ───────────────────────────────────────
        {
            bool hov = R(wx, wy, 77, 62, 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, 77, 62, sc, hov);
        }

        // ── Crafting-Label + 2×2 Grid (visuell) ─────────────────────────────
        McLabel(sb, "Crafting", wx, wy, 97, 8, sc);
        int[] cGx = { 98, 116,  98, 116 };
        int[] cGy = { 18,  18,  36,  36 };
        for (int i = 0; i < 4; i++)
        {
            bool hov = R(wx, wy, cGx[i], cGy[i], 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, cGx[i], cGy[i], sc, hov);
        }
        DrawCraftArrow(sb, wx, wy, sc);

        // Ergebnis-Slot
        {
            bool hov = R(wx, wy, 154, 28, 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, 154, 28, sc, hov);
        }

        // ── Haupt-Inventar (Slots 9–35, 3 Reihen × 9 Spalten) ───────────────
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 9; col++)
        {
            int idx = 9 + row * 9 + col;
            int gx  = 7 + col * 18, gy = 83 + row * 18;
            bool hov = R(wx, wy, gx, gy, 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, gx, gy, sc, hov);
            var item = inv.GetSlot(idx);
            if (!item.IsEmpty) DrawIconMc(sb, item, R(wx, wy, gx, gy, 18, 18, sc), sc);
        }

        // ── Hotbar-Zeile (Slots 0–8, y=141) ─────────────────────────────────
        for (int col = 0; col < 9; col++)
        {
            int gx = 7 + col * 18;
            bool hov = R(wx, wy, gx, 141, 18, 18, sc).Contains(mx, my);
            SlotG(sb, wx, wy, gx, 141, sc, hov);
            if (col == inv.SelectedSlot)
                DrawOutline(sb, R(wx, wy, gx, 141, 18, 18, sc), Color.White, sc);
            var item = inv.GetSlot(col);
            if (!item.IsEmpty) DrawIconMc(sb, item, R(wx, wy, gx, 141, 18, 18, sc), sc);
        }

        // ── Trennstrich zwischen 3×9-Grid und Hotbar ─────────────────────────
        int sepY = wy + 138 * sc;
        sb.Draw(_pixelTexture, new Rectangle(wx + 7 * sc, sepY, 162 * sc, sc), McDark * 0.5f);

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
        int wx = (sw - 176 * sc) / 2;
        int wy = (sh - 166 * sc) / 2;

        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 9; col++)
            if (R(wx, wy, 7 + col * 18, 83 + row * 18, 18, 18, sc).Contains(mx, my))
                return 9 + row * 9 + col;

        for (int col = 0; col < 9; col++)
            if (R(wx, wy, 7 + col * 18, 141, 18, 18, sc).Contains(mx, my))
                return col;

        return -1;
    }

    // ── Rüstungs-Slot-Symbole (Silhouetten) ──────────────────────────────────
    private static readonly string[] ArmorLabels = { "H", "C", "L", "B" };
    private void DrawArmorIcon(SpriteBatch sb, int wx, int wy, int gx, int gy, int sc, int idx)
    {
        // Kleines graues Symbol zentriert im Slot
        Color col = new Color(130, 130, 130) * 0.6f;
        // Vereinfachte Rüstungs-Icons (5×5 GUI-Pixel, zentriert in 18×18)
        int ox = wx + (gx + 6) * sc;
        int oy = wy + (gy + 6) * sc;
        int ps = sc; // 1 GUI-Pixel
        switch (idx)
        {
            case 0: // Helm: Bogen + Krempe
                sb.Draw(_pixelTexture, new Rectangle(ox + ps, oy,        4*ps, ps),    col);
                sb.Draw(_pixelTexture, new Rectangle(ox,       oy + ps,   6*ps, 3*ps), col);
                break;
            case 1: // Brust: Trapez
                sb.Draw(_pixelTexture, new Rectangle(ox + ps, oy,        4*ps, ps),    col);
                sb.Draw(_pixelTexture, new Rectangle(ox,       oy + ps,   6*ps, 4*ps), col);
                break;
            case 2: // Beine: zwei Säulen
                sb.Draw(_pixelTexture, new Rectangle(ox,       oy,        6*ps, 2*ps), col);
                sb.Draw(_pixelTexture, new Rectangle(ox,       oy + 2*ps, 2*ps, 3*ps), col);
                sb.Draw(_pixelTexture, new Rectangle(ox + 4*ps,oy + 2*ps, 2*ps, 3*ps), col);
                break;
            case 3: // Stiefel: zwei Blöcke
                sb.Draw(_pixelTexture, new Rectangle(ox,       oy,        2*ps, 4*ps), col);
                sb.Draw(_pixelTexture, new Rectangle(ox + 4*ps,oy,        2*ps, 4*ps), col);
                sb.Draw(_pixelTexture, new Rectangle(ox,       oy + 4*ps, 3*ps, ps),  col);
                sb.Draw(_pixelTexture, new Rectangle(ox + 3*ps,oy + 4*ps, 3*ps, ps),  col);
                break;
        }
    }

    // ── Spieler-Silhouette ────────────────────────────────────────────────────
    private void DrawPlayerSilhouette(SpriteBatch sb, int wx, int wy, int sc)
    {
        // Spieler-Bereich: x=51, y=8 → 36×58 GUI-Pixel
        Color bg  = new Color(60, 60, 60) * 0.25f;
        Color sil = new Color(80, 80, 80);

        // Hintergrund-Box: zwischen Rüstung (endet x=26) und Crafting (beginnt x=98)
        sb.Draw(_pixelTexture, R(wx, wy, 26, 8, 72, 66, sc), bg);

        // Figur zentriert in der 72-breiten Box (Zentrum x=62)
        // Kopf (8×8)
        sb.Draw(_pixelTexture, R(wx, wy, 58, 12, 8, 8,  sc), sil);
        // Körper (8×12)
        sb.Draw(_pixelTexture, R(wx, wy, 58, 21, 8, 12, sc), sil);
        // Linker Arm (4×12)
        sb.Draw(_pixelTexture, R(wx, wy, 54, 21, 4, 12, sc), sil * 0.85f);
        // Rechter Arm (4×12)
        sb.Draw(_pixelTexture, R(wx, wy, 66, 21, 4, 12, sc), sil * 0.85f);
        // Linkes Bein (4×18)
        sb.Draw(_pixelTexture, R(wx, wy, 58, 34, 4, 18, sc), sil);
        // Rechtes Bein (4×18)
        sb.Draw(_pixelTexture, R(wx, wy, 62, 34, 4, 18, sc), sil);
    }

    // ── Crafting-Pfeil (→) ────────────────────────────────────────────────────
    private void DrawCraftArrow(SpriteBatch sb, int wx, int wy, int sc)
    {
        Color c = new Color(100, 100, 100);
        // Körper: 14×3 horizontal bei y=31
        sb.Draw(_pixelTexture, R(wx, wy, 133, 31, 14, 3, sc), c);
        // Pfeilspitze: 4 abnehmende Schichten → echtes Dreieck
        sb.Draw(_pixelTexture, R(wx, wy, 147, 28, 2, 9, sc), c);
        sb.Draw(_pixelTexture, R(wx, wy, 149, 29, 2, 7, sc), c);
        sb.Draw(_pixelTexture, R(wx, wy, 151, 30, 2, 5, sc), c);
        sb.Draw(_pixelTexture, R(wx, wy, 153, 31, 1, 3, sc), c);
    }

    // ── Item-Icon (MC-Maßstab, 16×16 GUI-Pixel in 18×18 Slot) ───────────────
    private void DrawIconMc(SpriteBatch sb, ItemStack item, Rectangle slotRect, int sc,
        bool drawCount = true)
    {
        if (item.IsEmpty) return;

        int pad  = sc; // 1 GUI-Pixel Rand
        var dest = new Rectangle(slotRect.X + pad, slotRect.Y + pad,
                                 slotRect.Width - 2 * pad, slotRect.Height - 2 * pad);

        int ts = _blockAtlas.Width / 16;
        (int col, int row) = GetIconTile(item.Block);
        var src = new Rectangle(col * ts, row * ts, ts, ts);
        sb.Draw(_blockAtlas, dest, src, GetTint(item.Block));

        if (drawCount && item.Count > 1)
        {
            string s     = item.Count.ToString();
            float  fsc   = sc * 0.38f;
            Vector2 size = _font.MeasureString(s) * fsc;
            // Schatten
            sb.DrawString(_font, s,
                new Vector2(slotRect.Right - size.X - sc + sc * 0.5f,
                            slotRect.Bottom - size.Y - sc + sc * 0.5f),
                McDark * 0.7f, 0f, Vector2.Zero, fsc, SpriteEffects.None, 0f);
            // Text
            sb.DrawString(_font, s,
                new Vector2(slotRect.Right - size.X - sc, slotRect.Bottom - size.Y - sc),
                Color.White, 0f, Vector2.Zero, fsc, SpriteEffects.None, 0f);
        }
    }

    // ── Item-Icon für Hotbar (größere Slots) ─────────────────────────────────
    private void DrawIcon(SpriteBatch sb, ItemStack item, Rectangle slotRect, bool drawCount = true)
    {
        if (item.IsEmpty) return;
        int pad = slotRect.Width / 8;
        int ts  = _blockAtlas.Width / 16;
        (int col, int row) = GetIconTile(item.Block);
        sb.Draw(_blockAtlas,
            new Rectangle(slotRect.X + pad, slotRect.Y + pad,
                          slotRect.Width - pad * 2, slotRect.Height - pad * 2),
            new Rectangle(col * ts, row * ts, ts, ts), GetTint(item.Block));

        if (drawCount && item.Count > 1)
        {
            string s     = item.Count.ToString();
            float  fsc   = 0.55f;
            Vector2 size = _font.MeasureString(s) * fsc;
            sb.DrawString(_font, s,
                new Vector2(slotRect.Right - size.X - 2, slotRect.Bottom - size.Y - 2),
                Color.White, 0f, Vector2.Zero, fsc, SpriteEffects.None, 0f);
        }
    }

    private static (int col, int row) GetIconTile(BlockType block) => block switch
    {
        BlockType.Grass  => (0, 0),
        BlockType.Dirt   => (2, 0),
        BlockType.Stone  => (3, 0),
        BlockType.Wood   => (5, 0),
        BlockType.Leaves => (6, 0),
        BlockType.Sand   => (7, 0),
        BlockType.Water  => (8, 0),
        _                => (0, 0)
    };

    private static Color GetTint(BlockType block) => block switch
    {
        BlockType.Grass  => new Color(0x91, 0xBD, 0x59),
        BlockType.Leaves => new Color(0x77, 0xAB, 0x2F),
        _                => Color.White
    };

    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    private void DrawOutline(SpriteBatch sb, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(_pixelTexture, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
