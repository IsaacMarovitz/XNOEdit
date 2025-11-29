using ImGuiNET;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;

namespace XNOEdit.Panels
{
    public class ImGuiStagePanel
    {
        public event Action<int, bool> ToggleXnoVisibility;

        private readonly string _name;
        private readonly List<NinjaNext> _xnos;
        private readonly int _subobjectCount;
        private readonly int _meshSetCount;

        private Dictionary<int, bool> _visibilityState = new();

        public ImGuiStagePanel(string name, List<NinjaNext> xnos, bool[] visibility)
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

            for (int i = 0; i < _xnos.Count; i++)
            {
                ImGui.PushID(i);

                var xno = _xnos[i];
                var open = ImGui.Button(xno.Name);
                ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - ImGui.GetFrameHeight());
                var visible = GetVisibility(i);
                if (ImGui.Checkbox($"##VisibilityObject{i + 1}", ref visible))
                {
                    SetVisibility(i, visible);
                }
                ToggleXnoVisibility?.Invoke(i, visible);

                if (open)
                {

                }

                ImGui.PopID();
            }

            ImGui.End();
        }
    }
}
