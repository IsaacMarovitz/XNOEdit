using System.Collections.ObjectModel;
using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using Marathon.IO.Types.FileSystem;
using XNOEdit.Logging;

namespace XNOEdit.Panels
{
    public class MissionsPanel
    {
        public const string Name = "Missions";
        public event Action<IFile> LoadSet;

        private class TreeNode
        {
            public string Name { get; }
            public List<TreeNode> Children { get; } = [];
            public FileContainer? Container { get; }
            public ReadOnlyCollection<string>? MissionSet { get; }

            // Branch node
            public TreeNode(string name)
            {
                Name = name;
                Container = null;
            }

            // Leaf node
            public TreeNode(string name, ReadOnlyCollection<string> missionSet)
            {
                Name = name;
                Container = new FileContainer(name);
                MissionSet = missionSet;
            }

            public TreeNode Add(string name, params TreeNode[] children)
            {
                var node = new TreeNode(name);
                foreach (var child in children)
                    node.Children.Add(child);
                Children.Add(node);
                return this;
            }

            public IEnumerable<TreeNode> GetAllLeaves()
            {
                if (IsLeaf)
                    yield return this;

                foreach (var leaf in Children.SelectMany(child => child.GetAllLeaves()))
                    yield return leaf;
            }

            private bool IsLeaf => Container != null;
        }

        private readonly TreeNode _tree;
        private FileContainer? _selected;

        private string _searchText = "";

        public MissionsPanel()
        {
            Program.GameFolderLoaded += LoadGameFolderResources;
            _tree = BuildStageTree();
        }

        private static TreeNode BuildStageTree()
        {
            var root = new TreeNode("Stages");

            root.Add("Town",
                new TreeNode("Castle Town", MissionsMap.TwnAMissions),
                new TreeNode("New City", MissionsMap.TwnBMissions),
                new TreeNode("Forest", MissionsMap.TwnCMissions),
                new TreeNode("Circuit", MissionsMap.TwnDMissions));

            root.Add("Wave Ocean",
                new TreeNode("Section A", MissionsMap.WvoAMissions),
                new TreeNode("Section B", MissionsMap.WvoBMissions));

            root.Add("Dusty Desert",
                new TreeNode("Section A", MissionsMap.DtdAMissions),
                new TreeNode("Section B", MissionsMap.DtdBMissions));

            root.Add("White Acropolis",
                new TreeNode("Section A", MissionsMap.WapAMissions),
                new TreeNode("Section B", MissionsMap.WapBMissions));

            root.Add("Crisis City",
                new TreeNode("Section A", MissionsMap.CscAMissions),
                new TreeNode("Section B", MissionsMap.CscBMissions),
                new TreeNode("Section C", MissionsMap.CscCMissions),
                new TreeNode("Section E", MissionsMap.CscEMissions),
                new TreeNode("Section F", MissionsMap.CscFMissions));

            root.Add("Flame Core",
                new TreeNode("Section A", MissionsMap.FlcAMissions),
                new TreeNode("Section B", MissionsMap.FlcBMissions),
                new TreeNode("Section C", MissionsMap.FlcCMissions));

            root.Add("Radical Train",
                new TreeNode("Section A", MissionsMap.RctAMissions),
                new TreeNode("Section B", MissionsMap.RctBMissions));

            root.Add("Tropical Jungle",
                new TreeNode("Section A", MissionsMap.TpjAMissions),
                new TreeNode("Section B", MissionsMap.TpjBMissions),
                new TreeNode("Section C", MissionsMap.TpjCMissions));

            root.Add("Kingdom Valley",
                new TreeNode("Section A", MissionsMap.KdvAMissions),
                new TreeNode("Section B", MissionsMap.KdvBMissions),
                new TreeNode("Section C", MissionsMap.KdvCMissions),
                new TreeNode("Section D", MissionsMap.KdvDMissions));

            root.Add("Aquatic Base",
                new TreeNode("Section A", MissionsMap.AqaAMissions),
                new TreeNode("Section B", MissionsMap.AqaBMissions));

            root.Add("End of the World",
                new TreeNode("Section A", MissionsMap.EndAMissions));

            root.Add("Bosses",
                new TreeNode("Dusty Desert Arena", MissionsMap.Dr1DtdMissions),
                new TreeNode("White Acropolis Arena", MissionsMap.Dr1WapMissions),
                new TreeNode("Forest Arena", MissionsMap.Dr2Missions),
                new TreeNode("Radical Train Arena", MissionsMap.ShadowVsSilverMissions),
                new TreeNode("Egg Wyvern", MissionsMap.Dr3Missions),
                new TreeNode("Iblis 1", MissionsMap.FirstIblisMissions),
                new TreeNode("Iblis 2", MissionsMap.SecondIblisMissions),
                new TreeNode("Iblis 3", MissionsMap.ThirdIblisMissions),
                new TreeNode("Mephiles 1", MissionsMap.FirstMefMissions),
                new TreeNode("Mephiles 2", MissionsMap.SecondMefMissions),
                new TreeNode("Solaris", MissionsMap.SolarisMissions));

            return root;
        }

        public void LoadGameFolderResources()
        {
            var leaves = _tree.GetAllLeaves().ToList();

            foreach (var leaf in leaves)
                leaf.Container!.Clear();

            var scriptsArcPath = Path.Join(Configuration.GameFolder, "xenon", "archives", "scripts.arc");
            var scriptsArchive = new ArcFile(scriptsArcPath);

            foreach (var node in scriptsArchive.EnumerateFiles("*.set", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(node.Path);
                var leaf = leaves.FirstOrDefault(l => l.MissionSet!.Contains(name));

                if (leaf != null)
                    leaf.Container!.Add(node);
                else
                    Logger.Warning?.PrintMsg(LogClass.Application, $"Failed to categorise {name}.set");
            }
        }

        private void TriggerFileLoad(ImGuiComponents.File file, ReadOnlyCollection<IFile> files)
        {
            LoadSet?.Invoke(files.FirstOrDefault(x => x.Name == file.Identifier));
        }

        private void DrawTreeNode(TreeNode node)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var flags = ImGuiTreeNodeFlags.OpenOnArrow |
                        ImGuiTreeNodeFlags.OpenOnDoubleClick |
                        ImGuiTreeNodeFlags.NavLeftJumpsToParent |
                        ImGuiTreeNodeFlags.SpanFullWidth |
                        ImGuiTreeNodeFlags.DrawLinesToNodes;

            if (node.Children.Count == 0)
            {
                flags |= ImGuiTreeNodeFlags.Leaf;

                if (_selected == node.Container)
                    flags |= ImGuiTreeNodeFlags.Selected;
            }

            var open = ImGui.TreeNodeEx(node.Name, flags);

            if (ImGui.IsItemFocused())
                _selected = node.Container;

            if (open)
            {
                foreach (var child in node.Children)
                    DrawTreeNode(child);
                ImGui.TreePop();
            }
        }

        public void Render()
        {
            ImGui.Begin(Name, ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.BeginChild("Sidebar", new Vector2(150, 0), ImGuiChildFlags.ResizeX | ImGuiChildFlags.Borders | ImGuiChildFlags.NavFlattened);

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextWithHint("##search", "Search", ref _searchText, 256);

            if (ImGui.BeginTable("##table", 1, ImGuiTableFlags.RowBg))
            {
                foreach (var node in _tree.Children)
                    DrawTreeNode(node);

                ImGui.EndTable();
            }

            ImGui.EndChild();
            ImGui.SameLine();
            ImGui.BeginGroup();

            _selected?.Render(_searchText, TriggerFileLoad);

            ImGui.EndGroup();
            ImGui.End();
        }
    }
}
