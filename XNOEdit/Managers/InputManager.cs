using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace XNOEdit.Managers
{
    public enum SettingsToggle
    {
        WireframeMode,
        ShowGrid,
        BackfaceCulling,
        VertexColors,
        Lightmap,
        None
    }

    public class InputManager : IDisposable
    {
        public event Action ResetCameraAction;
        public event Action<SettingsToggle> SettingsChangedAction;
        public event Action<float, float> MouseMoveAction;
        public event Action<float> MouseScrollAction;

        public IKeyboard PrimaryKeyboard {  get; private set; }
        public IInputContext Input { get; private set; }

        private IWindow _window;
        private bool _mouseCaptured;
        private Vector2 _lastMousePosition;

        public void OnLoad(IWindow window)
        {
            _window = window;
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
            var toggle = SettingsToggle.None;

            switch (key)
            {
                case Key.F:
                    toggle = SettingsToggle.WireframeMode;
                    break;
                case Key.G:
                    toggle = SettingsToggle.ShowGrid;
                    break;
                case Key.C:
                    toggle = SettingsToggle.BackfaceCulling;
                    break;
                case Key.V:
                    toggle = SettingsToggle.VertexColors;
                    break;
                case Key.R:
                    ResetCameraAction?.Invoke();
                    break;
            }

            if (toggle != SettingsToggle.None)
                SettingsChangedAction?.Invoke(toggle);
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
