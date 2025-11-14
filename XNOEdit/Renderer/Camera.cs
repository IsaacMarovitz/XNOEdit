using System.Numerics;
using Silk.NET.Input;

namespace XNOEdit.Renderer
{
    public class Camera
    {
        public Transform Transform { get; set; } = new Transform();

        public float Fov { get; set; } = 60f;
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 10000.0f;
        public float MoveSpeed { get; set; } = 500.0f;
        public float LookSensitivity { get; set; } = 2f;
        public float DollySpeed { get; set; } = 100.0f;

        private float _yaw = -90f;
        private float _pitch = 0f;
        private Vector3 _front = new(0.0f, 0.0f, -1.0f);
        private Vector3 _up = Vector3.UnitY;
        private Vector3 _right = Vector3.UnitX;

        public Vector3 Front => _front;
        public Vector3 Up => _up;
        public Vector3 Right => _right;

        public Camera()
        {
            UpdateVectors();
        }

        public Matrix4x4 GetViewMatrix()
        {
            return Matrix4x4.CreateLookAt(Transform.Position, Transform.Position + _front, Vector3.UnitY);
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

        public void ProcessKeyboard(IKeyboard keyboard, float deltaTime)
        {
            var velocity = MoveSpeed * deltaTime;

            if (keyboard.IsKeyPressed(Key.W))
                Transform.Position += _front * velocity;

            if (keyboard.IsKeyPressed(Key.S))
                Transform.Position -= _front * velocity;

            if (keyboard.IsKeyPressed(Key.A))
                Transform.Position -= _right * velocity;

            if (keyboard.IsKeyPressed(Key.D))
                Transform.Position += _right * velocity;

            if (keyboard.IsKeyPressed(Key.Q))
                Transform.Position -= Vector3.UnitY * velocity;

            if (keyboard.IsKeyPressed(Key.E))
                Transform.Position += Vector3.UnitY * velocity;
        }

        public void ProcessMouseMove(float xOffset, float yOffset)
        {
            xOffset *= LookSensitivity;
            yOffset *= LookSensitivity;

            _yaw += xOffset;
            _pitch -= yOffset;

            _pitch = Math.Clamp(_pitch, -89.0f, 89.0f);

            UpdateVectors();
        }

        public void ProcessMouseScroll(float scrollY)
        {
            Transform.Position += _front * scrollY * DollySpeed;
        }

        public void LookAt(Vector3 target)
        {
            var direction = Vector3.Normalize(target - Transform.Position);

            _yaw = MathF.Atan2(direction.Z, direction.X) * 180f / MathF.PI;
            _pitch = MathF.Asin(direction.Y) * 180f / MathF.PI;

            UpdateVectors();
        }

        public void FrameTarget(Vector3 target, float distance)
        {
            Transform.Position = target + new Vector3(distance * 0.7f, distance * 0.5f, distance * 0.7f);
            LookAt(target);
        }

        private void UpdateVectors()
        {
            _front.X = MathF.Cos(_yaw * MathF.PI / 180f) * MathF.Cos(_pitch * MathF.PI / 180f);
            _front.Y = MathF.Sin(_pitch * MathF.PI / 180f);
            _front.Z = MathF.Sin(_yaw * MathF.PI / 180f) * MathF.Cos(_pitch * MathF.PI / 180f);
            _front = Vector3.Normalize(_front);

            _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY));
            _up = Vector3.Normalize(Vector3.Cross(_right, _front));
        }
    }
}

