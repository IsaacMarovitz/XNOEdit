using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using Marathon.Formats.Parameter;
using Marathon.IO.Types.FileSystem;

namespace XNOEdit.Panels
{
    public class ObjectsPanel
    {
        public event Action<IFile> LoadObject;
        public ObjectPhysicsParameterList ObjectParameters { get; private set; }

        private List<IFile> _enemyFiles;
        private List<IFile> _humanFiles;
        private List<IFile> _objectFiles;
        private List<IFile> _win32Files;
        private List<IFile> _setFiles;

        private string _searchText = "";

        public ObjectsPanel()
        {
            LoadGameFolderResources();
        }

        public void LoadGameFolderResources()
        {
            _enemyFiles = [];
            _humanFiles = [];
            _objectFiles = [];
            _win32Files = [];
            _setFiles = [];

            var enemyArcPath = Path.Join([
                Configuration.GameFolder,
                "xenon",
                "archives",
                "enemy.arc"
            ]);

            var humanArcPath = Path.Join([
                Configuration.GameFolder,
                "xenon",
                "archives",
                "human.arc"
            ]);

            var objectArcPath = Path.Join([
                Configuration.GameFolder,
                "xenon",
                "archives",
                "object.arc"
            ]);

            var enemyArchive = new ArcFile(enemyArcPath);
            foreach (var node in enemyArchive.EnumerateFiles("*.xno", SearchOption.AllDirectories))
            {
                _enemyFiles.Add(node);
            }

            var humanArchive = new ArcFile(humanArcPath);
            foreach (var node in humanArchive.EnumerateFiles("*.xno", SearchOption.AllDirectories))
            {
                _humanFiles.Add(node);
            }

            var objectArchive = new ArcFile(objectArcPath);
            foreach (var node in objectArchive.EnumerateFiles("*.xno", SearchOption.AllDirectories))
            {
                _objectFiles.Add(node);
            }

            var parametersFile = objectArchive.GetFile("/xenon/object/Common.bin");
            ObjectParameters = new ObjectPhysicsParameterList(parametersFile.Decompress());

            var win32Path = Path.Join([
                Configuration.GameFolder,
                "win32",
                "archives"
            ]);

            foreach (var file in Directory.EnumerateFiles(win32Path, "*.arc", SearchOption.AllDirectories))
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

                    _win32Files.Add(node);
                }
            }

            var scriptsArcPath = Path.Join([
                Configuration.GameFolder,
                "xenon",
                "archives",
                "scripts.arc"
            ]);

            var scriptsArchive = new ArcFile(scriptsArcPath);
            foreach (var node in scriptsArchive.EnumerateFiles("*.set", SearchOption.AllDirectories))
            {
                _setFiles.Add(node);
            }
        }

        private void TriggerFileLoad(ImGuiComponents.File file, List<IFile> files)
        {
            LoadObject?.Invoke(files.FirstOrDefault(x => x.Name == file.Identifier));
        }

        public void Render()
        {
            ImGui.Begin("Objects", ImGuiWindowFlags.AlwaysAutoResize);

            if (ImGui.BeginTabBar("Tab Bar", ImGuiTabBarFlags.AutoSelectNewTabs))
            {
                const float searchWidth = 200.0f;
                ImGui.SameLine(ImGui.GetWindowWidth() - searchWidth - ImGui.GetStyle().WindowPadding.X);
                ImGui.SetNextItemWidth(searchWidth);
                ImGui.InputTextWithHint("##search", "Search...", ref _searchText, 256);

                var objectFiles = _objectFiles.Select(x => new ImGuiComponents.File(x.Name, x.Name));
                ImGuiComponents.RenderFilesList("Objects", objectFiles, x =>
                {
                   TriggerFileLoad(x, _objectFiles);
                }, _searchText);

                var humanFiles = _humanFiles.Select(x => new ImGuiComponents.File(x.Name, x.Name));
                ImGuiComponents.RenderFilesList("Humans", humanFiles, x =>
                {
                    TriggerFileLoad(x, _humanFiles);
                }, _searchText);

                var enemyFiles = _enemyFiles.Select(x => new ImGuiComponents.File(x.Name, x.Name));
                ImGuiComponents.RenderFilesList("Enemies", enemyFiles, x =>
                {
                    TriggerFileLoad(x, _enemyFiles);
                }, _searchText);

                var win32Files = _win32Files.Select(x => new ImGuiComponents.File(x.Name, x.Name));
                ImGuiComponents.RenderFilesList("Misc.", win32Files, x =>
                {
                    TriggerFileLoad(x, _win32Files);
                }, _searchText);

                var setFiles = _setFiles.Select(x => new ImGuiComponents.File(x.Name, x.Name));
                ImGuiComponents.RenderFilesList("Sets", setFiles, x =>
                {
                    TriggerFileLoad(x, _setFiles);
                }, _searchText);

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }
}
