using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using Marathon.Formats.Parameter;
using Marathon.IO.Types.FileSystem;

namespace XNOEdit.Panels
{
    public class ObjectsPanel
    {
        public const string Name = "Objects";

        public event Action<IFile> LoadObject;
        public ObjectPhysicsParameterList PhysicsParameters { get; private set; }
        public PathObjParameterList PathParameters { get; private set; }

        private readonly FileContainer _enemy = new("Enemy");
        private readonly FileContainer _human = new("Human");
        private readonly FileContainer _object = new("Object");
        private readonly FileContainer _win32 = new("Misc.");

        private string _searchText = "";

        public ObjectsPanel()
        {
            Program.GameFolderLoaded += LoadGameFolderResources;
        }

        public void LoadGameFolderResources()
        {
            _enemy.Clear();
            _human.Clear();
            _object.Clear();
            _win32.Clear();

            _enemy.AddFromArc(ArcFiles.EnemyArc, "*.xno");
            _human.AddFromArc(ArcFiles.HumanArc, "*.xno");
            _object.AddFromArc(ArcFiles.ObjectArc, "*.xno");

            var commonFile = ArcFiles.ObjectArc.GetFile("/xenon/object/Common.bin");
            var pathObjFile = ArcFiles.ObjectArc.GetFile("/xenon/object/PathObj.bin");

            PhysicsParameters = new ObjectPhysicsParameterList(commonFile.Decompress());
            PathParameters = new PathObjParameterList(pathObjFile.Decompress());

            foreach (var file in Directory.EnumerateFiles(ArcFiles.Win32ArcFolder, "*.arc", SearchOption.AllDirectories))
            {
                var arc = new ArcFile(file);

                foreach (var node in arc.EnumerateFiles("*.xno", SearchOption.AllDirectories))
                {
                    if (node.Name.Contains("_CameraNull"))
                        continue;

                    if (node.Name.Contains("_EventSound"))
                        continue;

                    if (node.Name.Contains("_EventEffect"))
                        continue;

                    if (node.Name.Contains("_EventObject"))
                        continue;

                    _win32.Add(node);
                }
            }
        }

        private void TriggerFileLoad(ImGuiComponents.File file, IReadOnlyCollection<IFile> files)
        {
            LoadObject?.Invoke(files.FirstOrDefault(x => x.Name == file.Identifier));
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

                _object.RenderTabItem(_searchText, TriggerFileLoad);
                _human.RenderTabItem(_searchText, TriggerFileLoad);
                _enemy.RenderTabItem(_searchText, TriggerFileLoad);
                _win32.RenderTabItem(_searchText, TriggerFileLoad);

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }
}
