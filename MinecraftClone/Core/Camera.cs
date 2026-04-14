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
    private float _mouseSensitivity = 0.002f;

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
            _firstMouseMove = false;
            return;
        }
        float deltaX = mouseState.X - screenCenterX;
        float deltaY = mouseState.Y - screenCenterY;

        _yaw += deltaX * _mouseSensitivity;
        _pitch -= deltaY * _mouseSensitivity;
        _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);

        // Maus zurück in die Mitte setzen
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

    public void UpdateProjection(float aspectRatio)
    {
        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(70f),
            aspectRatio,
            0.1f,
            1000f
        );
    }
}
