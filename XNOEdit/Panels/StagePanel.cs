using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;

namespace XNOEdit.Panels
{
    public class StagePanel
    {
        public event Action<int, bool> ToggleXnoVisibility;
        public event Action<int, NinjaNext> ViewXno;

        private readonly string _name;
        private readonly List<NinjaNext> _xnos;
        private readonly int _subobjectCount;
        private readonly int _meshSetCount;

        private readonly Dictionary<int, bool> _visibilityState = new();

        public StagePanel(string name, List<NinjaNext> xnos, bool[] visibility)
        {
            _name = name;
            _xnos = xnos;

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

            for (var i = 0; i < visibility.Length; i++)
            {
                _visibilityState[i] = visibility[i];
            }
        }

        private bool GetVisibility(int xnoIndex)
        {
            return !_visibilityState.TryGetValue(xnoIndex, out var visible) || visible;
        }

        private void SetVisibility(int xnoIndex, bool visible)
        {
            _visibilityState[xnoIndex] = visible;
        }

        public void Render()
        {
            ImGui.Begin($"{_name}###StagePanel", ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.Text($"XNO Count: {_xnos.Count}");
            ImGui.Text($"Subobject Count: {_subobjectCount}");
            ImGui.Text($"Mesh Set Count: {_meshSetCount}");

            ImGui.SeparatorText("XNOs");

            for (var i = 0; i < _xnos.Count; i++)
            {
                ImGui.PushID(i);

                var visible = GetVisibility(i);
                if (ImGui.Checkbox($"##VisibilityObject{i + 1}", ref visible))
                {
                    SetVisibility(i, visible);
                    ToggleXnoVisibility?.Invoke(i, visible);
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
