using Hexa.NET.ImGui;
using Marathon.IO.Types.FileSystem;

namespace XNOEdit.Panels
{
    public class TextPanel
    {
        public const string Name = "Text";

        public event Action<IFile>? LoadTextBook;

        private readonly FileContainer _textBooks = new("Text Books");

        private string _searchText = "";

        public void LoadGameFolderResources()
        {
            _textBooks.Clear();
            _textBooks.AddFromArc(ArcFiles.TextArc, "*.mst");
        }

        private void TriggerTextBookLoad(ImGuiComponents.File file, IReadOnlyCollection<IFile> files)
        {
            LoadTextBook?.Invoke(files.FirstOrDefault(x => x.Name == file.Identifier));
        }

        public void Render()
        {
            ImGui.Begin(Name, ImGuiWindowFlags.AlwaysAutoResize);

            if (ImGui.BeginTabBar("Tab Bar", ImGuiTabBarFlags.AutoSelectNewTabs))
            {
                const float searchWidth = 200.0f;
                ImGui.SameLine(ImGui.GetWindowWidth() - searchWidth - ImGui.GetStyle().WindowPadding.X);
                ImGui.SetNextItemWidth(searchWidth);
                ImGui.InputTextWithHint("##search", "Search...", ref _searchText, 256);

                _textBooks.RenderTabItem(_searchText, TriggerTextBookLoad);

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }
}
