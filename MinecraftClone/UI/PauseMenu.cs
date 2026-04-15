using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.Core;

namespace MinecraftClone.UI;

public enum PauseMenuAction { None, Resume, SaveWorld, Quit }

public class PauseMenu
{
    public bool IsOpen { get; private set; }

    private enum Screen { Main, Settings }
    private Screen _screen = Screen.Main;

    private SpriteFont _font;
    private Texture2D  _pixel;

    // Konstanten
    private const int BtnW      = 280;
    private const int BtnH      = 44;
    private const int BtnSpacing = 10;
    private const int PanelPadX  = 32;
    private const int PanelPadY  = 24;

    public PauseMenu(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _font  = font;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Open()
    {
        IsOpen  = true;
        _screen = Screen.Main;
    }

    public void Close() => IsOpen = false;

    // ── Update: Mausklicks auswerten, Action zurückgeben ─────────────────────

    public PauseMenuAction Update(MouseState mouse, MouseState lastMouse,
        int screenWidth, int screenHeight, Camera camera)
    {
        if (!IsOpen) return PauseMenuAction.None;

        bool clicked = mouse.LeftButton == ButtonState.Pressed
                    && lastMouse.LeftButton == ButtonState.Released;
        int mx = mouse.X, my = mouse.Y;

        if (_screen == Screen.Main)
        {
            var btns = GetMainButtons(screenWidth, screenHeight);

            if (clicked)
            {
                if (btns[0].Contains(mx, my)) { Close(); return PauseMenuAction.Resume;    }
                if (btns[1].Contains(mx, my)) { _screen = Screen.Settings;                  }
                if (btns[2].Contains(mx, my)) { Close(); return PauseMenuAction.SaveWorld;  }
                if (btns[3].Contains(mx, my)) { Close(); return PauseMenuAction.Quit;       }
            }
        }
        else // Settings
        {
            HandleSettingsClicks(mx, my, clicked, screenWidth, screenHeight, camera);
        }

        return PauseMenuAction.None;
    }

    // ── Draw ─────────────────────────────────────────────────────────────────

    public void Draw(SpriteBatch sb, int screenWidth, int screenHeight, Camera camera)
    {
        if (!IsOpen) return;

        // Dunkles Overlay
        sb.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black * 0.55f);

        if (_screen == Screen.Main)
            DrawMain(sb, screenWidth, screenHeight);
        else
            DrawSettings(sb, screenWidth, screenHeight, camera);
    }

    // ── Hauptmenü ────────────────────────────────────────────────────────────

    private Rectangle[] GetMainButtons(int sw, int sh)
    {
        string[] labels = { "Weiterspielen", "Einstellungen", "Welt speichern", "Spiel beenden" };
        int totalH = labels.Length * BtnH + (labels.Length - 1) * BtnSpacing;
        int panelW = BtnW + 2 * PanelPadX;
        int panelH = 40 + totalH + 2 * PanelPadY; // 40 = title height
        int panelX = (sw - panelW) / 2;
        int panelY = (sh - panelH) / 2;

        var rects = new Rectangle[labels.Length];
        int startY = panelY + PanelPadY + 40;
        for (int i = 0; i < labels.Length; i++)
            rects[i] = new Rectangle(panelX + PanelPadX, startY + i * (BtnH + BtnSpacing), BtnW, BtnH);
        return rects;
    }

    private void DrawMain(SpriteBatch sb, int sw, int sh)
    {
        string[] labels = { "Weiterspielen", "Einstellungen", "Welt speichern", "Spiel beenden" };
        var btns = GetMainButtons(sw, sh);

        // Panel
        int panelW = BtnW + 2 * PanelPadX;
        int totalH = labels.Length * BtnH + (labels.Length - 1) * BtnSpacing;
        int panelH = 40 + totalH + 2 * PanelPadY;
        int panelX = (sw - panelW) / 2;
        int panelY = (sh - panelH) / 2;
        DrawPanel(sb, new Rectangle(panelX, panelY, panelW, panelH));

        // Titel
        DrawCenteredText(sb, "Pausenmenü", panelX, panelY + 8, panelW, 1.1f, Color.White);

        // Buttons
        var ms = Mouse.GetState();
        for (int i = 0; i < labels.Length; i++)
        {
            Color btnColor = i == 3 ? new Color(130, 40, 40) : new Color(65, 65, 65); // Quit rot
            DrawButton(sb, labels[i], btns[i], ms.X, ms.Y, btnColor);
        }
    }

    // ── Einstellungen ────────────────────────────────────────────────────────

    private void HandleSettingsClicks(int mx, int my, bool clicked,
        int sw, int sh, Camera camera)
    {
        var layout = GetSettingsLayout(sw, sh);
        if (!clicked) return;

        // Maus-Sensitivität
        if (layout.SensDown.Contains(mx, my))
            camera.MouseSensitivity = Math.Max(0.0001f, camera.MouseSensitivity - 0.0001f);
        if (layout.SensUp.Contains(mx, my))
            camera.MouseSensitivity = Math.Min(0.005f, camera.MouseSensitivity + 0.0001f);

        // FOV
        if (layout.FovDown.Contains(mx, my))
            camera.BaseFov = Math.Max(50f, camera.BaseFov - 5f);
        if (layout.FovUp.Contains(mx, my))
            camera.BaseFov = Math.Min(120f, camera.BaseFov + 5f);

        // Zurück
        if (layout.Back.Contains(mx, my))
            _screen = Screen.Main;
    }

    private record SettingsLayout(
        Rectangle SensDown, Rectangle SensUp,
        Rectangle FovDown,  Rectangle FovUp,
        Rectangle Back);

    private SettingsLayout GetSettingsLayout(int sw, int sh)
    {
        int panelW = BtnW + 2 * PanelPadX;
        int panelH = 220;
        int panelX = (sw - panelW) / 2;
        int panelY = (sh - panelH) / 2;

        int rowY1 = panelY + PanelPadY + 44;
        int rowY2 = rowY1 + 60;
        int backY = panelY + panelH - PanelPadY - BtnH;
        int cx    = panelX + PanelPadX;

        int arrowW = 38;
        int valW   = BtnW - 2 * (arrowW + 6);

        var sensDown = new Rectangle(cx,                         rowY1, arrowW, BtnH);
        var sensUp   = new Rectangle(cx + arrowW + 6 + valW + 6, rowY1, arrowW, BtnH);
        var fovDown  = new Rectangle(cx,                         rowY2, arrowW, BtnH);
        var fovUp    = new Rectangle(cx + arrowW + 6 + valW + 6, rowY2, arrowW, BtnH);
        var back     = new Rectangle(cx,                         backY, BtnW,   BtnH);

        return new SettingsLayout(sensDown, sensUp, fovDown, fovUp, back);
    }

    private void DrawSettings(SpriteBatch sb, int sw, int sh, Camera camera)
    {
        int panelW = BtnW + 2 * PanelPadX;
        int panelH = 220;
        int panelX = (sw - panelW) / 2;
        int panelY = (sh - panelH) / 2;
        DrawPanel(sb, new Rectangle(panelX, panelY, panelW, panelH));

        DrawCenteredText(sb, "Einstellungen", panelX, panelY + 8, panelW, 1.1f, Color.White);

        var layout = GetSettingsLayout(sw, sh);
        var ms     = Mouse.GetState();

        int arrowW = 38;
        int valW   = BtnW - 2 * (arrowW + 6);
        int cx     = panelX + PanelPadX;

        // Zeile 1: Maus-Sensitivität
        int sensPercent = (int)Math.Round(camera.MouseSensitivity / 0.0006f * 100f);
        DrawSettingsRow(sb, ms, "Maus-Sensitivität", $"{sensPercent}%",
            layout.SensDown, layout.SensUp, cx, layout.SensDown.Y, arrowW, valW);

        // Zeile 2: FOV
        DrawSettingsRow(sb, ms, "Sichtfeld (FOV)", $"{(int)camera.BaseFov}°",
            layout.FovDown, layout.FovUp, cx, layout.FovDown.Y, arrowW, valW);

        // Zurück-Button
        DrawButton(sb, "← Zurück", layout.Back, ms.X, ms.Y, new Color(65, 65, 65));
    }

    private void DrawSettingsRow(SpriteBatch sb, MouseState ms,
        string label, string value,
        Rectangle downBtn, Rectangle upBtn,
        int cx, int rowY, int arrowW, int valW)
    {
        // Label über der Zeile
        spriteBatchDrawLabel(sb, label, cx, rowY - 18);

        DrawButton(sb, "<", downBtn, ms.X, ms.Y, new Color(65, 65, 65));

        // Wert-Anzeige (mittig)
        var valRect = new Rectangle(downBtn.Right + 6, rowY, valW, BtnH);
        sb.Draw(_pixel, valRect, new Color(45, 45, 45));
        DrawRectOutline(sb, valRect, new Color(80, 80, 80), 1);
        DrawCenteredText(sb, value, valRect.X, valRect.Y + 12, valRect.Width, 0.85f, Color.White);

        DrawButton(sb, ">", upBtn, ms.X, ms.Y, new Color(65, 65, 65));
    }

    private void spriteBatchDrawLabel(SpriteBatch sb, string text, int x, int y)
    {
        sb.DrawString(_font, text, new Vector2(x, y),
            new Color(180, 180, 180), 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    // ── Zeichen-Helfer ───────────────────────────────────────────────────────

    private void DrawPanel(SpriteBatch sb, Rectangle rect)
    {
        sb.Draw(_pixel, rect, new Color(38, 38, 38) * 0.97f);
        DrawRectOutline(sb, rect, new Color(90, 90, 90), 2);
    }

    private void DrawButton(SpriteBatch sb, string label, Rectangle rect, int mx, int my, Color baseColor)
    {
        bool hovered = rect.Contains(mx, my);
        Color bg = hovered
            ? new Color(baseColor.R + 30, baseColor.G + 30, baseColor.B + 30)
            : baseColor;
        sb.Draw(_pixel, rect, bg);
        DrawRectOutline(sb, rect, hovered ? Color.White : new Color(100, 100, 100), 1);
        DrawCenteredText(sb, label, rect.X, rect.Y + rect.Height / 2 - 10, rect.Width, 0.8f, Color.White);
    }

    private void DrawCenteredText(SpriteBatch sb, string text, int panelX, int y, int panelW,
        float scale, Color color)
    {
        Vector2 size = _font.MeasureString(text) * scale;
        sb.DrawString(_font, text,
            new Vector2(panelX + (panelW - size.X) / 2f, y),
            color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch sb, Rectangle r, Color color, int t)
    {
        sb.Draw(_pixel, new Rectangle(r.X,         r.Y,          r.Width, t),       color);
        sb.Draw(_pixel, new Rectangle(r.X,         r.Bottom - t, r.Width, t),       color);
        sb.Draw(_pixel, new Rectangle(r.X,         r.Y,          t,       r.Height), color);
        sb.Draw(_pixel, new Rectangle(r.Right - t, r.Y,          t,       r.Height), color);
    }
}
