using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MinecraftClone.Core;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Forward { get; private set; }
    public Vector3 Up { get; private set; }
    public Vector3 Right { get; private set; }

    public Matrix ViewMatrix { get; private set; }
    public Matrix ProjectionMatrix { get; private set; }

    private float _yaw;
    private float _pitch;

    public float MouseSensitivity { get; set; } = 0.0006f;
    public float BaseFov          { get; set; } = 75f;

    private const float SprintFovBonus = 10f;
    private const float FovSpeed       = 8f;

    private float _currentFov = 75f;
    public bool IsSprinting { private get; set; }

    private bool _firstMouseMove = true;

    public Camera(Vector3 position, float aspectRatio)
    {
        Position = position;
        _yaw = 0f;
        _pitch = 0f;

        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(70f),
            aspectRatio,
            0.1f,
            1000f
        );

        UpdateVectors();
        UpdateViewMatrix();
    }

    public void Update(GameTime gameTime, GraphicsDevice graphicsDevice, bool captureMouseInput = true)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // FOV smooth anpassen (Sprint-Effekt wie Minecraft)
        float targetFov = IsSprinting ? BaseFov + SprintFovBonus : BaseFov;
        _currentFov += (targetFov - _currentFov) * MathHelper.Clamp(FovSpeed * deltaTime, 0f, 1f);
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(_currentFov),
            graphicsDevice.Viewport.Width / (float)graphicsDevice.Viewport.Height,
            0.1f, 1000f);

        if (!captureMouseInput)
        {
            UpdateVectors();
            UpdateViewMatrix();
            return;
        }

        // Mouse input - Maus in Bildschirmmitte zentrieren
        var mouseState = Mouse.GetState();
        int screenCenterX = graphicsDevice.Viewport.Width / 2;
        int screenCenterY = graphicsDevice.Viewport.Height / 2;

        if (_firstMouseMove)
        {
            Mouse.SetPosition(screenCenterX, screenCenterY);
            _firstMouseMove = false;
            UpdateVectors();
            UpdateViewMatrix();
            return;
        }

        // Delta is always relative to screen center — no _lastMouse tracking needed.
        // This avoids the warp race condition where SetPosition isn't applied yet
        // before the next GetState(), which caused inconsistent slow vs. fast movement.
        float deltaX = mouseState.X - screenCenterX;
        float deltaY = mouseState.Y - screenCenterY;

        _yaw += deltaX * MouseSensitivity;
        _pitch -= deltaY * MouseSensitivity;
        _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);

        Mouse.SetPosition(screenCenterX, screenCenterY);

        UpdateVectors();
        UpdateViewMatrix();
    }

    private void UpdateVectors()
    {
        Forward = new Vector3(
            (float)(Math.Cos(_pitch) * Math.Cos(_yaw)),
            (float)Math.Sin(_pitch),
            (float)(Math.Cos(_pitch) * Math.Sin(_yaw))
        );
        Forward = Vector3.Normalize(Forward);

        Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.Up));
        Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
    }

    private void UpdateViewMatrix()
    {
        ViewMatrix = Matrix.CreateLookAt(Position, Position + Forward, Up);
    }

    // Nur ViewMatrix neu berechnen (ohne Maus-Input) — wird benutzt wenn Inventar offen ist
    public void RefreshViewMatrix()
    {
        UpdateViewMatrix();
    }

    public void ResetMouseLock()
    {
        _firstMouseMove = true;
    }

    public void UpdateProjection(float aspectRatio)
    {
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(_currentFov),
            aspectRatio,
            0.1f,
            1000f
        );
    }
}
