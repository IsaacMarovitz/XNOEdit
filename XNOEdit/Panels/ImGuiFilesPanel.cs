using ImGuiNET;
using Marathon.Formats.Archive;
using Marathon.Formats.Parameter;
using Marathon.IO.Types.FileSystem;

namespace XNOEdit.Panels
{
    public class ImGuiFilesPanel
    {
        public event Action<IFile> LoadFile;
        public ObjectPhysicsParameterList ObjectParameters { get; private set; }

        private List<IFile> _enemyFiles;
        private List<IFile> _humanFiles;
        private List<IFile> _objectFiles;
        private List<IFile> _win32Files;
        private List<IFile> _setFiles;

        private string _searchText = "";

        public ImGuiFilesPanel()
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

        public void Render()
        {
            ImGui.Begin("Files", ImGuiWindowFlags.AlwaysAutoResize);

            if (ImGui.BeginTabBar("Tab Bar", ImGuiTabBarFlags.AutoSelectNewTabs))
            {
                RenderFilesList("Objects", _objectFiles);
                RenderFilesList("Humans", _humanFiles);
                RenderFilesList("Enemies", _enemyFiles);
                RenderFilesList("Misc.", _win32Files);
                RenderFilesList("Sets", _setFiles);

                // Add search bar on the right side of the tab bar
                ImGui.SameLine();
                var searchWidth = 200.0f;
                var availableSpace = ImGui.GetContentRegionAvail().X;
                if (availableSpace > searchWidth + 10)
                {
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + availableSpace - searchWidth);
                    ImGui.SetNextItemWidth(searchWidth);
                    ImGui.InputTextWithHint("##search", "Search...", ref _searchText, 256);
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void RenderFilesList(string title, List<IFile> files)
        {
            if (ImGui.BeginTabItem(title))
            {
                // Filter files based on search text
                var filteredFiles = string.IsNullOrWhiteSpace(_searchText)
                    ? files
                    : files.Where(f => f.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

                // Grid settings
                var thumbnailSize = 80.0f;
                var padding = 8.0f;
                var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                var textHeight = lineHeight * 2; // Space for 2 lines
                var itemHeight = thumbnailSize + textHeight + padding * 2;

                // Render scrollable grid
                ImGui.BeginChild("FileGrid", System.Numerics.Vector2.Zero, ImGuiChildFlags.None);

                var availWidth = ImGui.GetContentRegionAvail().X;
                var columns = Math.Max(1, (int)(availWidth / (thumbnailSize + padding)));

                // Calculate even spacing to fill remaining width
                var totalItemsWidth = columns * thumbnailSize;
                var remainingSpace = availWidth - totalItemsWidth;
                var gapSize = remainingSpace / (columns + 1);

                // Add top spacing to match left spacing
                var startY = ImGui.GetCursorPosY() + gapSize;

                for (var i = 0; i < filteredFiles.Count; i++)
                {
                    var file = filteredFiles[i];
                    var columnIndex = i % columns;
                    var rowIndex = i / columns;

                    // Calculate position for this item
                    var xPos = gapSize + columnIndex * (thumbnailSize + gapSize);
                    var yPos = startY + rowIndex * itemHeight;

                    ImGui.SetCursorPos(new System.Numerics.Vector2(xPos, yPos));

                    ImGui.PushID(i);

                    ImGui.BeginGroup();

                    // Thumbnail placeholder (as a button for interaction)
                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 1.0f));
                    var clicked = ImGui.Button("##thumb", new System.Numerics.Vector2(thumbnailSize, thumbnailSize));
                    ImGui.PopStyleColor();

                    // Create a child region to clip text
                    ImGui.BeginChild($"##text{i}", new System.Numerics.Vector2(thumbnailSize, textHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

                    // Estimate max characters for 2 lines
                    var avgCharWidth = ImGui.CalcTextSize("M").X;
                    var maxChars = (int)((thumbnailSize / avgCharWidth) * 2) - 3;

                    var displayName = file.Name;
                    if (displayName.Length > maxChars)
                    {
                        displayName = displayName[..maxChars] + "...";
                    }

                    // Center and wrap text
                    ImGui.PushTextWrapPos(thumbnailSize);

                    // Calculate wrapped text size
                    var wrappedSize = ImGui.CalcTextSize(displayName, thumbnailSize);

                    // Center horizontally only if it fits on one line
                    if (wrappedSize.X <= thumbnailSize)
                    {
                        var offset = (thumbnailSize - wrappedSize.X) * 0.5f;
                        ImGui.SetCursorPosX(offset);
                    }

                    ImGui.TextWrapped(displayName);
                    ImGui.PopTextWrapPos();

                    ImGui.EndChild();

                    ImGui.EndGroup();

                    // Tooltip with full path on hover
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(file.Name);
                    }

                    // Handle click
                    if (clicked || ImGui.IsItemClicked())
                    {
                        LoadFile?.Invoke(file);
                    }

                    ImGui.PopID();
                }

                ImGui.EndChild();
                ImGui.EndTabItem();
            }
        }
    }
}
