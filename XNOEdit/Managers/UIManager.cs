using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Silk.NET.WebGPU;
using XNOEdit.Logging;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Wgpu;
using XNOEdit.Services;

namespace XNOEdit.Managers
{
    public class UIManager : IDisposable
    {
        public event Action ResetCameraAction;

        public ViewportPanel? ViewportPanel { get; private set; }
        public ImGuiController? Controller { get; private set; }
        public ImGuiObjectsPanel? ObjectsPanel { get; private set; }
        public ImGuiXnoPanel? XnoPanel { get; private set; }
        public ImGuiStagePanel? StagePanel { get; private set; }
        public ImGuiStagesPanel? StagesPanel { get; private set; }
        public LoadProgress? CurrentLoadProgress { get; set; }

        public bool ViewportWantsInput => ViewportPanel?.IsHovered ?? false;

        private ImGuiAlertPanel? _alertPanel;

        private bool _firstLoop = true;
        private bool _xnoWindow = true;
        private bool _environmentWindow = true;
        private float _sunAzimuth;
        private float _sunAltitude;

        public void OnLoad(ImGuiController controller, WebGPU wgpu, WgpuDevice device)
        {
            Controller = controller;
            _alertPanel = new ImGuiAlertPanel();
            ObjectsPanel = new ImGuiObjectsPanel();
            StagesPanel = new ImGuiStagesPanel(this);
            ViewportPanel = new ViewportPanel(wgpu, device, controller);

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        }

        public void InitSunAngles(RenderSettings settings)
        {
            _sunAltitude = MathF.Asin(settings.SunDirection.Y) * 180.0f / MathF.PI;
            _sunAzimuth = MathF.Atan2(settings.SunDirection.Z, settings.SunDirection.X) * 180.0f / MathF.PI;

            if (_sunAzimuth < 0)
                _sunAzimuth += 360.0f;
        }

        public void InitXnoPanel(NinjaNext xno, Action<int, bool> toggleSubobjectVisibility, Action<int, int, bool> toggleMeshSetVisibility)
        {
            StagePanel = null;
            XnoPanel = new ImGuiXnoPanel(xno);
            XnoPanel.ToggleSubobjectVisibility += toggleSubobjectVisibility;
            XnoPanel.ToggleMeshSetVisibility += toggleMeshSetVisibility;
        }

        public void InitStagePanel(string name, List<NinjaNext> xnos, bool[] visibility, Action<int, bool> toggleXnoVisibility)
        {
            XnoPanel = null;
            StagePanel = new ImGuiStagePanel(name, xnos, visibility);
            StagePanel.ToggleXnoVisibility += toggleXnoVisibility;
        }

        public unsafe void OnRender(double deltaTime, ref RenderSettings settings, RenderPassEncoder* pass, TextureManager textureManager)
        {
            Controller?.Update((float)deltaTime);

            var flags = ImGuiDockNodeFlags.NoDockingOverCentralNode;
            var dockspaceId = ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), flags);

            if (_firstLoop)
            {
                ImGuiP.DockBuilderRemoveNode(dockspaceId);
                var central = ImGuiP.DockBuilderAddNode(dockspaceId,
                    flags | (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.Space);

                var viewport = ImGui.GetMainViewport();
                ImGuiP.DockBuilderSetNodeSize(central, viewport.WorkSize);
                ImGuiP.DockBuilderSetNodePos(central, viewport.WorkPos);

                var remainingId = dockspaceId;

                var leftDock = ImGuiP.DockBuilderSplitNode(remainingId, ImGuiDir.Left, 0.2f, null, &remainingId);
                var bottomDock = ImGuiP.DockBuilderSplitNode(remainingId, ImGuiDir.Down, 0.3f, null, &remainingId);
                var centralNode = ImGuiP.DockBuilderGetNode(remainingId);
                centralNode.LocalFlags |= (ImGuiDockNodeFlags)(ImGuiDockNodeFlagsPrivate.CentralNode | ImGuiDockNodeFlagsPrivate.HiddenTabBar) | ImGuiDockNodeFlags.NoUndocking;

                ImGuiP.DockBuilderDockWindow("Viewport", remainingId);
                ImGuiP.DockBuilderDockWindow("Environment", leftDock);
                ImGuiP.DockBuilderDockWindow("###XnoPanel", leftDock);
                ImGuiP.DockBuilderDockWindow("###StagePanel", leftDock);
                ImGuiP.DockBuilderDockWindow("Objects", bottomDock);
                ImGuiP.DockBuilderDockWindow("Stages", bottomDock);

                ImGuiP.DockBuilderFinish(dockspaceId);

                _firstLoop = false;
            }

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Render"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("Show Grid", "G", ref settings.ShowGrid);
                    ImGui.MenuItem("Vertex Colors", "V", ref settings.VertexColors);
                    ImGui.MenuItem("Backface Culling", "C", ref settings.BackfaceCulling);
                    ImGui.MenuItem("Wireframe", "F", ref settings.WireframeMode);
                    ImGui.MenuItem("Lightmap", "", ref settings.Lightmap);

                    ImGui.Separator();

                    if (ImGui.MenuItem("Reset Camera", "R"))
                    {
                        ResetCameraAction.Invoke();
                    }

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("XNO", "", ref _xnoWindow);
                    ImGui.MenuItem("Environment", "", ref _environmentWindow);

                    ImGui.Separator();

                    var debugLogs = Configuration.DebugLogs;
                    ImGui.MenuItem("Debug Logs", "", ref debugLogs);

                    if (debugLogs != Configuration.DebugLogs)
                    {
                        Configuration.DebugLogs = debugLogs;
                        Logger.SetEnable(LogLevel.Debug, debugLogs);
                    }

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            if (_environmentWindow)
            {
                ImGui.Begin("Environment");
                ImGui.SliderFloat("Camera Sensitivity", ref settings.CameraSensitivity, 0.0f, 1.0f);
                ImGui.SeparatorText("Sun");
                ImGui.ColorEdit3("Color", ref settings.SunColor, ImGuiColorEditFlags.NoInputs);

                var editedAzimuth = ImGui.SliderFloat("Azimuth", ref _sunAzimuth, 0.0f, 360.0f, "%.1f°");
                var editedAltitude = ImGui.SliderFloat("Altitude", ref _sunAltitude, 0.0f, 90.0f, "%.1f°");

                if (editedAzimuth || editedAltitude)
                {
                    var azimuthRad = _sunAzimuth * MathF.PI / 180.0f;
                    var altitudeRad = _sunAltitude * MathF.PI / 180.0f;

                    settings.SunDirection = new Vector3(
                        (float)(Math.Cos(altitudeRad) * Math.Cos(azimuthRad)),
                        (float)Math.Sin(altitudeRad),
                        (float)(Math.Cos(altitudeRad) * Math.Sin(azimuthRad)));
                }

                ImGui.End();
            }

            if (_xnoWindow)
                XnoPanel?.Render(textureManager);

            StagePanel?.Render();
            ObjectsPanel?.Render();
            StagesPanel?.Render();

            ViewportPanel?.Render();

            RenderLoadingOverlay();
            _alertPanel?.Render(deltaTime);
            Controller?.Render(pass);
        }

        private void RenderLoadingOverlay()
        {
            var progress = CurrentLoadProgress;
            if (progress == null || progress.Stage == LoadStage.Complete)
                return;

            var viewport = ImGui.GetMainViewport();
            var windowFlags = ImGuiWindowFlags.NoDecoration |
                              ImGuiWindowFlags.NoMove |
                              ImGuiWindowFlags.NoSavedSettings |
                              ImGuiWindowFlags.NoFocusOnAppearing |
                              ImGuiWindowFlags.NoNav;

            var windowWidth = 400f;
            var windowHeight = 80f;
            var padding = 20f;

            ImGui.SetNextWindowPos(new Vector2(
                viewport.WorkPos.X + viewport.WorkSize.X - windowWidth - padding,
                viewport.WorkPos.Y + viewport.WorkSize.Y - windowHeight - padding
            ));
            ImGui.SetNextWindowSize(new Vector2(windowWidth, windowHeight));
            ImGui.SetNextWindowBgAlpha(0.85f);

            if (ImGui.Begin("##LoadingOverlay", windowFlags))
            {
                ImGui.Text(progress.Message);

                if (progress.IsIndeterminate)
                {
                    var time = (float)(DateTime.Now.TimeOfDay.TotalSeconds % 2.0) / 2.0f;
                    var barWidth = 0.3f;
                    var position = (float)(time * (1.0 + barWidth) - barWidth);

                    ImGui.ProgressBar(0f, new Vector2(-1, 0), "");

                    var drawList = ImGui.GetWindowDrawList();
                    var cursorPos = ImGui.GetItemRectMin();
                    var itemSize = ImGui.GetItemRectSize();

                    var startX = cursorPos.X + Math.Max(0, position) * itemSize.X;
                    var endX = cursorPos.X + Math.Min(1, position + barWidth) * itemSize.X;

                    drawList.AddRectFilled(
                        new Vector2(startX, cursorPos.Y),
                        new Vector2(endX, cursorPos.Y + itemSize.Y),
                        ImGui.GetColorU32(ImGuiCol.PlotHistogram)
                    );
                }
                else
                {
                    var progressText = $"{progress.Current}/{progress.Total}";
                    ImGui.ProgressBar(progress.Percentage, new Vector2(-1, 0), progressText);
                }

                ImGui.End();
            }
        }

        public void Dispose()
        {
            ViewportPanel?.Dispose();
            Controller?.Dispose();
        }

        public void TriggerAlert(AlertLevel alertLevel, string alert)
        {
            if (alert != string.Empty)
                _alertPanel?.TriggerAlert(alertLevel, alert);
        }

        public void LoadGameFolderResources()
        {
            ObjectsPanel?.LoadGameFolderResources();
        }
    }
}
