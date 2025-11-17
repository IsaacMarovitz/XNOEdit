using System.Numerics;
using ImGuiNET;

namespace XNOEdit.Panels
{
    public class ImGuiAlertPanel
    {
        private const float MessageTime = 2;
        private float _timer = MessageTime;
        private string _currentMessage;

        public void TriggerAlert(string message)
        {
            _timer = 0;
            _currentMessage = message;
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

                ImGui.Begin("Alert", ImGuiWindowFlags.NoMove
                                     | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                                     | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus);
                ImGui.Text(_currentMessage);
                ImGui.End();
            }
        }
    }
}
