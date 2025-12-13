using System.Collections.ObjectModel;
using Hexa.NET.ImGui;
using Marathon.Formats.Archive;
using XNOEdit.Managers;

namespace XNOEdit.Panels
{
    public class StagesPanel
    {
        public const string Name = "Stages";
        public event Action<ArcFile> LoadStage;

        private readonly UIManager _uiManager;

        public StagesPanel(UIManager uiManager)
        {
            _uiManager = uiManager;
        }

        private void TriggerFileLoad(ImGuiComponents.File file, ReadOnlyDictionary<string, string> files)
        {
            var arcName = files.FirstOrDefault(x => x.Value == file.Identifier).Value;
            var arcPath = Path.Join(
                Configuration.GameFolder,
                "win32",
                "archives",
                $"{arcName}.arc"
            );

            try
            {
                var stageArc = new ArcFile(arcPath);
                LoadStage?.Invoke(stageArc);
                _uiManager.TriggerAlert(AlertLevel.Info, $"Loaded {arcName}.arc");
            }
            catch (Exception ex)
            {
                _uiManager.TriggerAlert(AlertLevel.Warning, $"Unable to load {arcName}.arc: \"{ex.Message}\"");
            }
        }

        public void Render()
        {
            ImGui.Begin(Name, ImGuiWindowFlags.AlwaysAutoResize);

            if (ImGui.BeginTabBar("Stages", ImGuiTabBarFlags.AutoSelectNewTabs))
            {
                var stages = StagesMap.Stages.Select(x => new ImGuiComponents.File(x.Key, x.Value));
                ImGuiComponents.RenderFilesListTabItem("Stages", stages, x =>
                {
                    TriggerFileLoad(x, StagesMap.Stages);
                });

                var bosses = StagesMap.Bosses.Select(x => new ImGuiComponents.File(x.Key, x.Value));
                ImGuiComponents.RenderFilesListTabItem("Bosses", bosses, x =>
                {
                    TriggerFileLoad(x, StagesMap.Bosses);
                });

                var events = StagesMap.Events.Select(x => new ImGuiComponents.File(x.Key, x.Value));
                ImGuiComponents.RenderFilesListTabItem("Events", events, x =>
                {
                    TriggerFileLoad(x, StagesMap.Events);
                });

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }
}
