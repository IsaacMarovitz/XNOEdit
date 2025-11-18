using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace XNOEdit.Managers
{
    public class InputManager : IDisposable
    {
        public event Action ResetCameraAction;
        public event Action<RenderSettings, string> SettingsChangedAction;
        public event Action<float, float> MouseMoveAction;
        public event Action<float> MouseScrollAction;

        public IKeyboard PrimaryKeyboard {  get; private set; }
        public IInputContext Input { get; private set; }

        private IWindow _window;
        private RenderSettings _settings;
        private bool _mouseCaptured;
        private Vector2 _lastMousePosition;

        public void OnLoad(IWindow window, RenderSettings settings)
        {
            _window = window;
            _settings = settings;
            Input = _window.CreateInput();

            PrimaryKeyboard = Input.Keyboards.FirstOrDefault();

            if (PrimaryKeyboard != null)
            {
                PrimaryKeyboard.KeyDown += KeyDown;
            }

            foreach (var mouse in Input.Mice)
            {
                mouse.Cursor.CursorMode = CursorMode.Normal;
                mouse.MouseMove += OnMouseMove;
                mouse.Scroll += OnMouseWheel;
                mouse.MouseDown += OnMouseDown;
                mouse.MouseUp += OnMouseUp;
            }
        }

        private void KeyDown(IKeyboard keyboard, Key key, int keyCode)
        {
            if (key == Key.Escape)
                _window.Close();

            var alert = string.Empty;

            switch (key)
            {
                case Key.F:
                    _settings.WireframeMode = !_settings.WireframeMode;
                    alert = $"Wireframe Mode: {(_settings.WireframeMode ? "ON" : "OFF")}";
                    break;
                case Key.G:
                    _settings.ShowGrid = !_settings.ShowGrid;
                    alert = $"Grid: {(_settings.ShowGrid ? "ON" : "OFF")}";
                    break;
                case Key.C:
                    _settings.BackfaceCulling = !_settings.BackfaceCulling;
                    alert = $"Backface Culling: {(_settings.BackfaceCulling ? "ON" : "OFF")}";
                    break;
                case Key.V:
                    _settings.VertexColors = !_settings.VertexColors;
                    alert = $"Vertex Colors: {(_settings.VertexColors ? "ON" : "OFF")}";
                    break;
                case Key.R:
                    ResetCameraAction?.Invoke();
                    alert = "Camera Reset";
                    break;
            }

            SettingsChangedAction?.Invoke(_settings, alert);
        }

        private void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (!_mouseCaptured) return;

            var lookSensitivity = 0.1f;
            if (_lastMousePosition == default)
            {
                _lastMousePosition = position;
            }
            else
            {
                var xOffset = (position.X - _lastMousePosition.X) * lookSensitivity;
                var yOffset = (position.Y - _lastMousePosition.Y) * lookSensitivity;
                _lastMousePosition = position;

                MouseMoveAction?.Invoke(xOffset, yOffset);
            }
        }

        private void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            if (!ImGui.GetIO().WantCaptureMouse)
                MouseScrollAction?.Invoke(scrollWheel.Y);
        }

        private void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (button != MouseButton.Left || ImGui.GetIO().WantCaptureMouse)
            {
                return;
            }

            _mouseCaptured = true;
            mouse.Cursor.CursorMode = CursorMode.Raw;
            _lastMousePosition = default;
        }

        private void OnMouseUp(IMouse mouse, MouseButton button)
        {
            if (button != MouseButton.Left || ImGui.GetIO().WantCaptureMouse)
            {
                return;
            }

            _mouseCaptured = false;
            mouse.Cursor.CursorMode = CursorMode.Normal;
        }

        public void Dispose()
        {
            Input?.Dispose();
        }
    }
}
