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
        public event Action<IFile> LoadMission;

        private class TreeNode
        {
            public string Name { get; }
            public List<TreeNode> Children { get; } = [];
            public FileContainer? Container { get; }
            public MissionGroup MissionGroup { get; }

            // Branch node
            public TreeNode(string name)
            {
                Name = name;
                Container = null;
            }

            // Leaf node
            public TreeNode(string name, MissionGroup missionGroup)
            {
                Name = name;
                Container = new FileContainer(name);
                MissionGroup = missionGroup;
            }

            public TreeNode Add(string name, params TreeNode[] children)
            {
                var node = new TreeNode(name);
                node.Children.AddRange(children);

                Children.Add(node);
                return this;
            }

            public TreeNode Add(params TreeNode[] children)
            {
                Children.AddRange(children);
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
                new TreeNode("Castle Town", MissionsMap.TwnAMissionGroup),
                new TreeNode("New City", MissionsMap.TwnBMissionGroup),
                new TreeNode("Forest", MissionsMap.TwnCMissionGroup),
                new TreeNode("Circuit", MissionsMap.TwnDMissionGroup));

            root.Add("Wave Ocean",
                new TreeNode("Section A", MissionsMap.WvoAMissionGroup),
                new TreeNode("Section B", MissionsMap.WvoBMissionGroup));

            root.Add("Dusty Desert",
                new TreeNode("Section A", MissionsMap.DtdAMissionGroup),
                new TreeNode("Section B", MissionsMap.DtdBMissionGroup));

            root.Add("White Acropolis",
                new TreeNode("Section A", MissionsMap.WapAMissionGroup),
                new TreeNode("Section B", MissionsMap.WapBMissionGroup));

            root.Add("Crisis City",
                new TreeNode("Section A", MissionsMap.CscAMissionGroup),
                new TreeNode("Section B", MissionsMap.CscBMissionGroup),
                new TreeNode("Section C", MissionsMap.CscCMissionGroup),
                new TreeNode("Section E", MissionsMap.CscEMissionGroup),
                new TreeNode("Section F", MissionsMap.CscFMissionGroup));

            root.Add("Flame Core",
                new TreeNode("Section A", MissionsMap.FlcAMissionGroup),
                new TreeNode("Section B", MissionsMap.FlcBMissionGroup),
                new TreeNode("Section C", MissionsMap.FlcCMissionGroup));

            root.Add("Radical Train",
                new TreeNode("Section A", MissionsMap.RctAMissionGroup),
                new TreeNode("Section B", MissionsMap.RctBMissionGroup));

            root.Add("Tropical Jungle",
                new TreeNode("Section A", MissionsMap.TpjAMissionGroup),
                new TreeNode("Section B", MissionsMap.TpjBMissionGroup),
                new TreeNode("Section C", MissionsMap.TpjCMissionGroup));

            root.Add("Kingdom Valley",
                new TreeNode("Section A", MissionsMap.KdvAMissionGroup),
                new TreeNode("Section B", MissionsMap.KdvBMissionGroup),
                new TreeNode("Section C", MissionsMap.KdvCMissionGroup),
                new TreeNode("Section D", MissionsMap.KdvDMissionGroup));

            root.Add("Aquatic Base",
                new TreeNode("Section A", MissionsMap.AqaAMissionGroup),
                new TreeNode("Section B", MissionsMap.AqaBMissionGroup));

            root.Add("Bosses",
                new TreeNode("Dusty Desert Arena", MissionsMap.Dr1DtdMissionGroup),
                new TreeNode("White Acropolis Arena", MissionsMap.Dr1WapMissionGroup),
                new TreeNode("Forest Arena", MissionsMap.Dr2MissionGroup),
                new TreeNode("Radical Train Arena", MissionsMap.ShadowVsSilverMissionGroup),
                new TreeNode("Egg Wyvern", MissionsMap.Dr3MissionGroup),
                new TreeNode("Iblis 1", MissionsMap.FirstIblisMissionGroup),
                new TreeNode("Iblis 2", MissionsMap.SecondIblisMissionGroup),
                new TreeNode("Iblis 3", MissionsMap.ThirdIblisMissionGroup),
                new TreeNode("Mephiles 1", MissionsMap.FirstMefMissionGroup),
                new TreeNode("Mephiles 2", MissionsMap.SecondMefMissionGroup),
                new TreeNode("Solaris", MissionsMap.SolarisMissionGroup));

            root.Add(new TreeNode("Misc.", MissionsMap.MiscMissionGroup));

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
                var leaf = leaves.FirstOrDefault(l => l.MissionGroup.Missions.Contains(name));

                if (leaf != null)
                    leaf.Container!.Add(node);
                else
                    Logger.Warning?.PrintMsg(LogClass.Application, $"Failed to categorise {name}.set");
            }
        }

        private void TriggerFileLoad(ImGuiComponents.File file, ReadOnlyCollection<IFile> files)
        {
            LoadMission?.Invoke(files.FirstOrDefault(x => x.Name == file.Identifier));
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
