using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MinecraftClone.Core;

public enum CameraMode { FirstPerson, ThirdPersonBack, ThirdPersonFront }

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Forward { get; private set; }
    public Vector3 Up { get; private set; }
    public Vector3 Right { get; private set; }

    public Matrix ViewMatrix     { get; private set; }
    public Matrix ProjectionMatrix { get; private set; }

    public CameraMode Mode         { get; set; } = CameraMode.FirstPerson;
    public float      Yaw          => _yaw;
    public float      Pitch        => _pitch;
    public Vector3    ViewPosition { get; private set; }

    private float _yaw;
    private float _pitch;

    public float MouseSensitivity { get; set; } = 0.002618f; // Minecraft 100% default
    public float BaseFov          { get; set; } = 70f;

    private const float SprintFovBonus = 10f;
    private const float FovSpeed       = 8f;

    private float _currentFov = 70f;
    public bool IsSprinting { private get; set; }

    private bool _firstMouseMove  = true;
    private bool _skipMouseFrame  = false;
    private int  _anchorX, _anchorY;

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

        // Mouse input
        var mouseState = Mouse.GetState();
        int screenCenterX = graphicsDevice.Viewport.Width  / 2;
        int screenCenterY = graphicsDevice.Viewport.Height / 2;

        // Beim ersten Frame (Start / nach Menü): Cursor zentrieren, nächsten Frame skippen
        if (_firstMouseMove)
        {
            Mouse.SetPosition(screenCenterX, screenCenterY);
            _firstMouseMove = false;
            _skipMouseFrame = true;
            UpdateVectors();
            UpdateViewMatrix();
            return;
        }

        // Frame nach SetPosition skippen — Cursor-Position erst dann verlässlich lesen
        if (_skipMouseFrame)
        {
            _skipMouseFrame = false;
            _anchorX = mouseState.X;
            _anchorY = mouseState.Y;
            UpdateVectors();
            UpdateViewMatrix();
            return;
        }

        float deltaX = mouseState.X - _anchorX;
        float deltaY = mouseState.Y - _anchorY;
        _anchorX = mouseState.X;
        _anchorY = mouseState.Y;

        _yaw += deltaX * MouseSensitivity;
        _pitch -= deltaY * MouseSensitivity;
        _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);

        // Cursor neu zentrieren wenn er den Bildschirmrand erreicht
        int margin = 80;
        if (mouseState.X < margin || mouseState.X > screenCenterX * 2 - margin ||
            mouseState.Y < margin || mouseState.Y > screenCenterY * 2 - margin)
        {
            Mouse.SetPosition(screenCenterX, screenCenterY);
            _skipMouseFrame = true;
        }

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
        const float Dist = 4f;
        switch (Mode)
        {
            case CameraMode.ThirdPersonBack:
                ViewPosition = Position - Forward * Dist;
                ViewMatrix   = Matrix.CreateLookAt(ViewPosition, Position, Vector3.Up);
                break;
            case CameraMode.ThirdPersonFront:
                ViewPosition = Position + Forward * Dist;
                ViewMatrix   = Matrix.CreateLookAt(ViewPosition, Position, Vector3.Up);
                break;
            default:
                ViewPosition = Position;
                ViewMatrix   = Matrix.CreateLookAt(Position, Position + Forward, Up);
                break;
        }
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
