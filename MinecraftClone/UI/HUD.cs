using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MinecraftClone.Gameplay;

namespace MinecraftClone.UI;

public class HUD
{
    private SpriteFont _font;
    private Texture2D _pixelTexture;
    private GraphicsDevice _graphicsDevice;

    // Geglättete FPS-Anzeige
    private float _smoothFps = 0f;

    // Pixel-Art Herz-Muster (8 Spalten × 6 Zeilen)
    private static readonly bool[,] HeartPattern =
    {
        { false, true,  true,  false, false, true,  true,  false },
        { true,  true,  true,  true,  true,  true,  true,  true  },
        { true,  true,  true,  true,  true,  true,  true,  true  },
        { false, true,  true,  true,  true,  true,  true,  false },
        { false, false, true,  true,  true,  true,  false, false },
        { false, false, false, true,  true,  false, false, false },
    };

    private const float HeartPixelSize = 3.0f;                                 // Jedes "Pixel" = 3×3 Bildschirmpixel
    private const int HeartCols       = 8;
    private const int HeartRows       = 6;
    private static readonly int HeartW = (int)(HeartCols * HeartPixelSize);   // ~17px
    private static readonly int HeartH = (int)(HeartRows * HeartPixelSize);   // ~13px
    private const int HeartSpacing    = 3;
    private const int TotalHearts     = 10;

    public HUD(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _graphicsDevice = graphicsDevice;
        _font = font;

        _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, Player player, Inventory inventory,
        int screenWidth, int screenHeight, GameTime gameTime)
    {
        // FPS glätten (exponentieller Durchschnitt)
        float currentFps = gameTime.ElapsedGameTime.TotalSeconds > 0
            ? (float)(1.0 / gameTime.ElapsedGameTime.TotalSeconds)
            : _smoothFps;
        _smoothFps = _smoothFps * 0.85f + currentFps * 0.15f;

        spriteBatch.Begin();

        DrawCrosshair(spriteBatch, screenWidth, screenHeight);
        DrawHotbar(spriteBatch, inventory, screenWidth, screenHeight, out int hotbarY, out int hotbarStartX);
        DrawHearts(spriteBatch, player, hotbarStartX, hotbarY);
        DrawFps(spriteBatch, screenWidth);
        DrawDebugInfo(spriteBatch, player, 10, 10);

        spriteBatch.End();
    }

    // ─── Crosshair ───────────────────────────────────────────────────────────

    private void DrawCrosshair(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
    {
        int cx = screenWidth / 2;
        int cy = screenHeight / 2;
        const int size = 10;
        const int thick = 2;

        spriteBatch.Draw(_pixelTexture,
            new Rectangle(cx - size, cy - thick / 2, size * 2, thick), Color.White);
        spriteBatch.Draw(_pixelTexture,
            new Rectangle(cx - thick / 2, cy - size, thick, size * 2), Color.White);
    }

    // ─── Hotbar ──────────────────────────────────────────────────────────────

    private void DrawHotbar(SpriteBatch spriteBatch, Inventory inventory,
        int screenWidth, int screenHeight, out int hotbarY, out int hotbarStartX)
    {
        const int slotSize  = 62;
        const int spacing   = 5;
        const int slots     = 9;

        int totalWidth = (slotSize + spacing) * slots - spacing;
        int startX = (screenWidth - totalWidth) / 2;
        hotbarY       = screenHeight - slotSize - 10;
        hotbarStartX  = startX;

        for (int i = 0; i < slots; i++)
        {
            int x = startX + (slotSize + spacing) * i;
            Color slotColor = i == inventory.SelectedSlot ? Color.White : Color.Gray;

            // Hintergrund
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x, hotbarY, slotSize, slotSize), slotColor * 0.45f);

            // Rahmen
            DrawRectangleOutline(spriteBatch,
                new Rectangle(x, hotbarY, slotSize, slotSize), slotColor, 2);

            // Block-Name
            if (inventory.Hotbar[i] != World.BlockType.Air)
            {
                string name = inventory.Hotbar[i].ToString();
                Vector2 textSize = _font.MeasureString(name);
                spriteBatch.DrawString(_font, name,
                    new Vector2(x + slotSize / 2f - textSize.X * 0.35f,
                                y: hotbarY + slotSize / 2f - textSize.Y * 0.35f),
                    Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            }


        }
    }

    // ─── Herzen ──────────────────────────────────────────────────────────────

    private void DrawHearts(SpriteBatch spriteBatch, Player player, int hotbarStartX, int hotbarY)
    {
        const int gap = 6; // Abstand zwischen Herzen und Hotbar

        int startX = hotbarStartX;  // Linksbündig mit der Hotbar
        int y      = hotbarY - HeartH - gap;

        float hp = player.Health; // 0 – 20

        for (int i = 0; i < TotalHearts; i++)
        {
            int x          = startX + i * (HeartW + HeartSpacing);
            float heartHp  = hp - i * 2f; // 2 = ein volles Herz

            // Leeres Herz (Umriss) immer zeichnen
            DrawHeartPixels(spriteBatch, x, y, full: true,  color: new Color(30, 0, 0));

            if (heartHp >= 2f)
            {
                // Volles Herz
                DrawHeartPixels(spriteBatch, x, y, full: true, color: Color.Red);
            }
            else if (heartHp >= 1f)
            {
                // Halbes Herz – nur linke 4 Spalten
                DrawHeartPixels(spriteBatch, x, y, full: false, color: Color.Red);
            }
        }
    }

    /// <param name="full">true = alle Spalten; false = nur linke Hälfte</param>
    private void DrawHeartPixels(SpriteBatch spriteBatch, int x, int y, bool full, Color color)
    {
        int colLimit = full ? HeartCols : HeartCols / 2;

        for (int row = 0; row < HeartRows; row++)
        {
            for (int col = 0; col < colLimit; col++)
            {
                if (!HeartPattern[row, col]) continue;

                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(
                        x + (int)(col * HeartPixelSize),
                        y + (int)(row * HeartPixelSize),
                        (int)System.Math.Ceiling(HeartPixelSize),
                        (int)System.Math.Ceiling(HeartPixelSize)),
                    color);
            }
        }
    }

    // ─── FPS ─────────────────────────────────────────────────────────────────

    private void DrawFps(SpriteBatch spriteBatch, int screenWidth)
    {
        string text = $"FPS: {(int)_smoothFps}";
        Vector2 size = _font.MeasureString(text) * 0.75f;
        spriteBatch.DrawString(_font, text,
            new Vector2(screenWidth - size.X - 10, 10),
            Color.White, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
    }

    // ─── Debug ───────────────────────────────────────────────────────────────

    private void DrawDebugInfo(SpriteBatch spriteBatch, Player player, int x, int y)
    {
        string posText = $"Pos: {player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1}";
        spriteBatch.DrawString(_font, posText,
            new Vector2(x, y), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        string velText = $"Vel: {player.Velocity.X:F1}, {player.Velocity.Y:F1}, {player.Velocity.Z:F1}";
        spriteBatch.DrawString(_font, velText,
            new Vector2(x, y + 20), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    // ─── Hilfsmethoden ───────────────────────────────────────────────────────

    private void DrawRectangleOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
    }
}
