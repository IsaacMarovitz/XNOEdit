using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Marathon.Formats.Placement;
using XNOEdit.Services;

namespace XNOEdit.Panels
{
    public class MissionPanel
    {
        public const string Name = "Mission";
        public event Action<int, NinjaNext>? ViewXno;

        private readonly string _name;
        private readonly StageSet _stageSet;
        private List<LoadedObjectGroup> _loadedGroups;
        private HashSet<string> _failedTypes;

        public MissionPanel(string name, StageSet stageSet, List<LoadedObjectGroup> loadedGroups, HashSet<string> failedTypes)
        {
            _name = name;
            _stageSet = stageSet;
            _loadedGroups = loadedGroups;
            _failedTypes = failedTypes;
        }

        public void Render()
        {
            ImGui.Begin(Name, ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.65f);

            ImGui.Text($"Name: {_name}");

            if (ImGui.BeginTabBar("Tab Bar"))
            {
                RenderObjects();

                RenderGroups();

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void RenderObjects()
        {
            if (ImGui.BeginTabItem("Objects"))
            {
                var groups = _stageSet.Objects
                    .Select((setObject, index) => (setObject, index))
                    .GroupBy(x => x.setObject.Type)
                    .OrderBy(g => g.Key);

                foreach (var group in groups)
                {
                    if (!ImGui.CollapsingHeader($"{group.Key}"))
                        continue;

                    ImGui.Indent();

                    foreach (var (setObject, i) in group.OrderBy(x => x.setObject.Name))
                    {
                        ImGui.PushID(i);

                        if (ImGui.CollapsingHeader($"{setObject.Name}"))
                        {
                            ImGui.Text($"Type: {setObject.Type}");

                            var position = setObject.Position;
                            ImGuiComponents.InputFloat3("Position", ref position, "%.1f", ImGuiInputTextFlags.ReadOnly);

                            // TODO: Perhaps Euler angles
                            var rotation = setObject.Rotation.AsVector4();
                            ImGuiComponents.InputFloat4("Rotation", ref rotation, "%.1f", ImGuiInputTextFlags.ReadOnly);

                            var drawDistance = setObject.DrawDistance;
                            ImGuiComponents.InputFloat("Draw Distance", ref drawDistance, "%.1f",
                                ImGuiInputTextFlags.ReadOnly);

                            var startInactive = setObject.StartInactive;
                            ImGuiComponents.Checkbox("Start Inactive", ref startInactive);

                            ImGui.Indent();

                            if (ImGui.CollapsingHeader("Parameters"))
                            {
                                foreach (var parameter in setObject.Parameters)
                                {
                                    ImGui.Text($"{parameter.Type}: {parameter.Value}");
                                }
                            }

                            ImGui.Unindent();
                        }

                        ImGui.PopID();
                    }

                    ImGui.Unindent();
                }

                ImGui.EndTabItem();
            }
        }

        private void RenderGroups()
        {
            if (ImGui.BeginTabItem("Groups"))
            {
                for (var i = 0; i < _stageSet.Groups.Count; i++)
                {
                    ImGui.PushID(i);

                    var group = _stageSet.Groups[i];

                    if (ImGui.CollapsingHeader($"{group.Name}"))
                    {
                        ImGui.Indent();
                        ImGui.Text($"Function: {group.Function}");

                        if (ImGui.CollapsingHeader("Object IDs"))
                        {
                            foreach (var objectId in group.Objects)
                            {
                                ImGui.BulletText($"{objectId}");
                            }
                        }

                        ImGui.Unindent();
                    }

                    ImGui.PopID();
                }

                ImGui.EndTabItem();
            }
        }
    }
}
