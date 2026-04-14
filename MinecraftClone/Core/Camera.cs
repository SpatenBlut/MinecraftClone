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
    private float _mouseSensitivity = 0.0006f;

    private const float NormalFov  = 75f;
    private const float SprintFov  = 85f;
    private const float FovSpeed   = 8f;    // Grad pro Sekunde (smooth wie Minecraft)

    private float _currentFov = NormalFov;
    public bool IsSprinting { private get; set; }

    private bool _firstMouseMove = true;
    private int  _lastMouseX;
    private int  _lastMouseY;

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

    public void Update(GameTime gameTime, GraphicsDevice graphicsDevice)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Mouse input - Maus in Bildschirmmitte zentrieren
        var mouseState = Mouse.GetState();
        int screenCenterX = graphicsDevice.Viewport.Width / 2;
        int screenCenterY = graphicsDevice.Viewport.Height / 2;

        if (_firstMouseMove)
        {
            Mouse.SetPosition(screenCenterX, screenCenterY);
            // Direkt nach SetPosition nochmal lesen — speichert die echte Position
            // die das OS gesetzt hat (nicht die gewünschte), damit kein Delta-Fehler entsteht
            var s = Mouse.GetState();
            _lastMouseX = s.X;
            _lastMouseY = s.Y;
            _firstMouseMove = false;
            return;
        }

        // Delta gegen LETZTE BEKANNTE Position berechnen (nicht gegen berechnetes Zentrum)
        // → verhindert Drift bei sehr hohem FPS wenn SetPosition noch nicht vom OS verarbeitet wurde
        float deltaX = mouseState.X - _lastMouseX;
        float deltaY = mouseState.Y - _lastMouseY;

        _yaw += deltaX * _mouseSensitivity;
        _pitch -= deltaY * _mouseSensitivity;
        _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);

        // Maus zurück in die Mitte setzen, dann sofort auslesen
        Mouse.SetPosition(screenCenterX, screenCenterY);
        var warped = Mouse.GetState();
        _lastMouseX = warped.X;
        _lastMouseY = warped.Y;

        // FOV smooth anpassen (Sprint-Effekt wie Minecraft)
        float targetFov = IsSprinting ? SprintFov : NormalFov;
        _currentFov += (targetFov - _currentFov) * MathHelper.Clamp(FovSpeed * deltaTime, 0f, 1f);
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(_currentFov),
            graphicsDevice.Viewport.Width / (float)graphicsDevice.Viewport.Height,
            0.1f, 1000f);

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
