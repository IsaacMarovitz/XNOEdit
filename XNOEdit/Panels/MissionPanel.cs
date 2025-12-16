using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;

namespace XNOEdit.Panels
{
    public class MissionPanel
    {
        public const string Name = "Mission";
        public event Action<int, NinjaNext> ViewXno;

        private readonly string _name;

        public MissionPanel(string name)
        {
            _name = name;
        }

        public void Render()
        {
            ImGui.Begin(Name, ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.65f);

            ImGui.Text($"Name: {_name}");

            ImGui.End();
        }
    }
}
