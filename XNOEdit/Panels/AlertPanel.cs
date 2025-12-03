using System.Numerics;
using Hexa.NET.ImGui;
using XNOEdit.Logging;

namespace XNOEdit.Panels
{
    public enum AlertLevel
    {
        Info,
        Warning,
        Error,
    }

    public class AlertPanel
    {
        private const float MessageTime = 2;
        private float _timer = MessageTime;
        private string _currentMessage;
        private AlertLevel _currentAlertLevel = AlertLevel.Info;

        public void TriggerAlert(AlertLevel level, string message)
        {
            Logger.Info?.PrintMsg(LogClass.Application, message);

            _timer = 0;
            _currentMessage = message;
            _currentAlertLevel = level;
        }

        public void Render(double deltaTime)
        {
            if (_timer < MessageTime)
            {
                _timer += (float)deltaTime;

                var io = ImGui.GetIO();
                var padding = 10.0f;
                var windowPos = new Vector2(io.DisplaySize.X - padding, io.DisplaySize.Y - padding);
                var size = ImGui.CalcTextSize(_currentMessage);

                ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, new  Vector2(1.0f, 1.0f));
                ImGui.SetNextWindowSize(new Vector2(size.X + 18.0f, size.Y + 40.0f), ImGuiCond.Always);

                var name = _currentAlertLevel switch
                {
                    AlertLevel.Info => "Info",
                    AlertLevel.Warning => "Warning",
                    AlertLevel.Error => "Error",
                    _ => "Alert"
                };

                var color = _currentAlertLevel switch
                {
                    AlertLevel.Warning => new Vector4(0.81f, 0.67f, 0.0f, 1.0f),
                    AlertLevel.Error => new Vector4(0.75f, 0.14f, 0.14f, 1.0f),
                    _ => Vector4.Zero
                };

                if (color != Vector4.Zero)
                    ImGui.PushStyleColor(ImGuiCol.TitleBg, color);

                ImGui.Begin(name, ImGuiWindowFlags.NoMove
                                  | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                                  | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus);

                if (color != Vector4.Zero)
                    ImGui.PopStyleColor();

                ImGui.Text(_currentMessage);
                ImGui.End();
            }
        }
    }
}
