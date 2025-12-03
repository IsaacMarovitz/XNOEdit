using System.Numerics;
using Hexa.NET.ImGui;
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
        private Vector2 _captureStartPosition;
        private UIManager _uiManager;

        public InputManager(UIManager uiManager)
        {
            _uiManager = uiManager;
        }

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
            if (ImGui.GetIO().WantCaptureKeyboard)
                return;

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

            var xOffset = (position.X - _captureStartPosition.X) * lookSensitivity;
            var yOffset = (position.Y - _captureStartPosition.Y) * lookSensitivity;

            mouse.Position = _captureStartPosition;

            if (xOffset != 0 || yOffset != 0)
                MouseMoveAction?.Invoke(xOffset, yOffset);
        }

        private void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            if (!_uiManager.ViewportWantsInput)
                MouseScrollAction?.Invoke(scrollWheel.Y);
        }

        private void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (button != MouseButton.Left)
                return;

            if (!_uiManager.ViewportWantsInput)
                return;

            _mouseCaptured = true;
            _captureStartPosition = mouse.Position;
            mouse.Cursor.CursorMode = CursorMode.Hidden;
        }

        private void OnMouseUp(IMouse mouse, MouseButton button)
        {
            if (button != MouseButton.Left)
                return;

            _mouseCaptured = false;
            mouse.Cursor.CursorMode = CursorMode.Normal;
        }

        public void Dispose()
        {
            Input?.Dispose();
        }
    }
}
