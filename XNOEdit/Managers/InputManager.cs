using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using SDL3;
using XNOEdit.Panels;
using XNOEdit.Render;

namespace XNOEdit.Managers
{
    public class InputManager(nint window, UIManager ui, SceneView view, RenderSettings settings)
    {
        private bool _mouseCaptured;
        private Vector2 _captureStartPosition;

        public void Update(float deltaTime)
        {
            if (ui.ViewportWantsInput || !ImGui.GetIO().WantCaptureKeyboard)
                view.Camera.ProcessKeyboard(deltaTime, settings.CameraSensitivity);
        }

        public void HandleEvent(in SDL.Event @event)
        {
            switch (@event.Type)
            {
                case (uint)SDL.EventType.TextInput:
                    if (Marshal.PtrToStringUTF8(@event.Text.Text) is { } input)
                        ui.Controller?.UpdateImguiInput(input);
                    break;
                case (uint)SDL.EventType.KeyDown:
                    ui.Controller?.UpdateImGuiKey(@event.Key.Key, true);
                    ui.Controller?.UpdateImGuiKeyModifiers(@event.Key.Mod);

                    if ((ui.ViewportWantsInput && !ImGui.GetIO().WantCaptureKeyboard) || _mouseCaptured) {
                        view.Camera.UpdateKeyDown(@event.Key.Key);
                    } else if (view.Camera.IsKeyDown(@event.Key.Key))
                    {
                        view.Camera.UpdateKeyUp(@event.Key.Key);
                    }

                    if (ImGui.GetIO().WantCaptureKeyboard) break;

                    var toggle = @event.Key.Key switch
                    {
                        SDL.Keycode.F => SettingsToggle.WireframeMode,
                        SDL.Keycode.G => SettingsToggle.ShowGrid,
                        SDL.Keycode.C => SettingsToggle.BackfaceCulling,
                        _ => SettingsToggle.None
                    };

                    if (toggle != SettingsToggle.None)
                        OnRenderSettingsChanged(toggle);

                    if (@event.Key.Key == SDL.Keycode.R)
                    {
                        ui.TriggerAlert(AlertLevel.Info, "Camera Reset");
                        view.ResetCamera();
                    }

                    break;
                case (uint)SDL.EventType.KeyUp:
                    view.Camera.UpdateKeyUp(@event.Key.Key);
                    ui.Controller?.UpdateImGuiKey(@event.Key.Key, false);
                    ui.Controller?.UpdateImGuiKeyModifiers(@event.Key.Mod);
                    break;
                case (uint)SDL.EventType.MouseMotion:
                    ui.Controller?.UpdateImGuiMouseMove(@event.Motion.X, @event.Motion.Y);

                    if (!_mouseCaptured) break;

                    var lookSensitivity = 0.1f;

                    var xOffset = (@event.Motion.X - _captureStartPosition.X) * lookSensitivity;
                    var yOffset = (@event.Motion.Y - _captureStartPosition.Y) * lookSensitivity;

                    SDL.WarpMouseInWindow(window, _captureStartPosition.X, _captureStartPosition.Y);

                    if (xOffset != 0 || yOffset != 0)
                        view.Camera.OnMouseMove(xOffset, yOffset);

                    break;
                case (uint)SDL.EventType.MouseWheel:
                    ui.Controller?.UpdateImGuiMouseWheel(@event.Wheel.X, @event.Wheel.Y);

                    if (ui.ViewportWantsInput)
                        view.Camera.ProcessMouseScroll(@event.Wheel.Y, settings.CameraSensitivity);

                    break;
                case (uint)SDL.EventType.MouseButtonDown:
                    ui.Controller?.UpdateImGuiMouse(@event.Button.Button, true);

                    if (@event.Button.Button != SDL.ButtonLeft)
                        break;

                    if (!ui.ViewportWantsInput)
                        break;

                    _mouseCaptured = true;
                    _captureStartPosition = new Vector2(@event.Button.X, @event.Button.Y);
                    SDL.SetWindowRelativeMouseMode(window, true);

                    break;
                case (uint)SDL.EventType.MouseButtonUp:
                    ui.Controller?.UpdateImGuiMouse(@event.Button.Button, false);

                    if (@event.Button.Button != SDL.ButtonLeft)
                        break;

                    _mouseCaptured = false;
                    SDL.SetWindowRelativeMouseMode(window, false);

                    break;
            }
        }

        private void OnRenderSettingsChanged(SettingsToggle toggle)
        {
            var alert = string.Empty;

            switch (toggle)
            {
                case SettingsToggle.ShowGrid:
                    settings.ShowGrid = !settings.ShowGrid;
                    alert = $"Grid: {(settings.ShowGrid ? "ON" : "OFF")}";
                    break;
                case SettingsToggle.BackfaceCulling:
                    settings.BackfaceCulling = !settings.BackfaceCulling;
                    alert = $"Backface Culling: {(settings.BackfaceCulling ? "ON" : "OFF")}";
                    break;
            }

            ui.TriggerAlert(AlertLevel.Info, alert);
        }
    }

    public enum SettingsToggle
    {
        WireframeMode,
        ShowGrid,
        BackfaceCulling,
        None
    }
}
