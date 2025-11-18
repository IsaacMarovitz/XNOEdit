using System.Numerics;
using Silk.NET.Input;

namespace XNOEdit.Renderer
{
    public class Camera
    {
        public Vector3 Position { get; private set; }
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 10000.0f;

        private static float Fov => 60f;
        private static float MoveSpeed => 2.0f;
        private static float DollySpeed => 0.5f;
        private static float LookSensitivity => 2f;

        private float _yaw = -90f;
        private float _pitch = 0f;
        private Vector3 _front = new(0.0f, 0.0f, -1.0f);
        private Vector3 _frontHorizontal = new(0.0f, 0.0f, -1.0f);
        private Vector3 _right = Vector3.UnitX;
        private float _modelRadius = 1.0f;

        public Camera()
        {
            UpdateVectors();
        }

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Position, Position + _front, Vector3.UnitY);
        }

        public Matrix4x4 GetProjectionMatrix(float aspectRatio)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(
                Fov * MathF.PI / 180f,
                aspectRatio,
                NearPlane,
                FarPlane
            );
        }

        public void SetModelRadius(float radius)
        {
            _modelRadius = radius;
        }

        public void ProcessKeyboard(IKeyboard keyboard, float deltaTime)
        {
            var velocity = MoveSpeed * deltaTime * _modelRadius;

            if (keyboard.IsKeyPressed(Key.W))
                Position += _frontHorizontal * velocity;

            if (keyboard.IsKeyPressed(Key.S))
                Position -= _frontHorizontal * velocity;

            if (keyboard.IsKeyPressed(Key.A))
                Position -= _right * velocity;

            if (keyboard.IsKeyPressed(Key.D))
                Position += _right * velocity;

            if (keyboard.IsKeyPressed(Key.Q))
                Position -= Vector3.UnitY * velocity;

            if (keyboard.IsKeyPressed(Key.E))
                Position += Vector3.UnitY * velocity;
        }

        public void OnMouseMove(float xOffset, float yOffset)
        {
            xOffset *= LookSensitivity;
            yOffset *= LookSensitivity;

            _yaw += xOffset;
            _pitch -= yOffset;

            _pitch = Math.Clamp(_pitch, -89.0f, 89.0f);

            UpdateVectors();
        }

        public void OnMouseScroll(float scrollY)
        {
            Position += _front * scrollY * DollySpeed * _modelRadius;
        }

        public void FrameTarget(Vector3 target, float distance)
        {
            Position = target + new Vector3(distance * 0.7f, distance * 0.5f, distance * 0.7f);
            LookAt(target);
        }

        private void LookAt(Vector3 target)
        {
            var direction = Vector3.Normalize(target - Position);

            _yaw = MathF.Atan2(direction.Z, direction.X) * 180f / MathF.PI;
            _pitch = MathF.Asin(direction.Y) * 180f / MathF.PI;

            UpdateVectors();
        }

        private void UpdateVectors()
        {
            _front.X = MathF.Cos(_yaw * MathF.PI / 180f) * MathF.Cos(_pitch * MathF.PI / 180f);
            _front.Y = MathF.Sin(_pitch * MathF.PI / 180f);
            _front.Z = MathF.Sin(_yaw * MathF.PI / 180f) * MathF.Cos(_pitch * MathF.PI / 180f);
            _front = Vector3.Normalize(_front);

            _frontHorizontal.X = MathF.Cos(_yaw * MathF.PI / 180f);
            _frontHorizontal.Y = 0f;
            _frontHorizontal.Z = MathF.Sin(_yaw * MathF.PI / 180f);
            _frontHorizontal = Vector3.Normalize(_frontHorizontal);

            _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY));
        }
    }
}

