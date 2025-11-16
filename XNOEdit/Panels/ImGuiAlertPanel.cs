using System.Numerics;
using ImGuiNET;

namespace XNOEdit.Panels
{
    public class ImGuiAlertPanel
    {
        private const float MessageTime = 2;
        private float timer = MessageTime;
        private string currentMessage;

        public void TriggerAlert(string message)
        {
            Console.WriteLine($"[ALERT]: {message}");

            timer = 0;
            currentMessage = message;
        }

        public void Render(double deltaTime)
        {
            if (timer < MessageTime)
            {
                timer += (float)deltaTime;

                var io = ImGui.GetIO();
                var padding = 10.0f;
                var windowPos = new Vector2(io.DisplaySize.X - padding, io.DisplaySize.Y - padding);
                var size = ImGui.CalcTextSize(currentMessage);

                ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, new  Vector2(1.0f, 1.0f));
                ImGui.SetNextWindowSize(new Vector2(size.X + 18.0f, size.Y + 40.0f), ImGuiCond.Always);

                ImGui.Begin("Alert", ImGuiWindowFlags.NoMove
                                     | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                                     | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus);
                ImGui.Text(currentMessage);
                ImGui.End();
            }
        }
    }
}
