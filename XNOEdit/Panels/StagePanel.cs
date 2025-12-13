using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using XNOEdit.Services;

namespace XNOEdit.Panels
{
    public class StagePanel
    {
        public const string Name = "Stage";
        public event Action<int, NinjaNext> ViewXno;

        private readonly string _name;
        private readonly List<NinjaNext> _xnos;
        private readonly int _subobjectCount;
        private readonly int _meshSetCount;
        private readonly ISceneVisibility _visibility;

        public StagePanel(string name, List<NinjaNext> xnos, ISceneVisibility visibility)
        {
            _name = name;
            _xnos = xnos;
            _visibility = visibility;

            foreach (var xno in _xnos)
            {
                var objectChunk = xno.GetChunk<ObjectChunk>();

                if (objectChunk != null)
                {
                    _subobjectCount += objectChunk.SubObjects.Count;

                    foreach (var subObject in objectChunk.SubObjects)
                    {
                        _meshSetCount += subObject.MeshSets.Count;
                    }
                }
            }
        }


        public void Render()
        {
            ImGui.Begin(Name, ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.65f);

            ImGui.Text($"Name: {_name}");
            ImGui.Text($"XNO Count: {_xnos.Count}");
            ImGui.Text($"Subobject Count: {_subobjectCount}");
            ImGui.Text($"Mesh Set Count: {_meshSetCount}");

            ImGui.SeparatorText("XNOs");

            for (var i = 0; i < _xnos.Count; i++)
            {
                ImGui.PushID(i);

                var visible = _visibility.GetXnoVisible(i);
                if (ImGuiComponents.StyledCheckbox($"##VisibilityObject{i + 1}", visible))
                {
                    visible = !visible;
                    _visibility.SetXnoVisible(i, visible);
                }

                ImGui.SameLine();

                // Style the button like a Collapsing Header
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.Header]);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderHovered]);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.HeaderActive]);
                ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));

                if (ImGui.Button(_xnos[i].Name, new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                {
                    ViewXno?.Invoke(i, _xnos[i]);
                }

                ImGui.PopStyleVar();
                ImGui.PopStyleColor(3);
                ImGui.PopID();
            }

            ImGui.End();
        }
    }
}
