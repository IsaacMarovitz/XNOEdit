using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Silk.NET.WebGPU;
using XNOEdit.Fonts;
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
        public ObjectsPanel? ObjectsPanel { get; private set; }
        public XnoPanel? XnoPanel { get; private set; }
        public StagePanel? StagePanel { get; private set; }
        public StagesPanel? StagesPanel { get; private set; }
        public LoadProgress? CurrentLoadProgress { get; set; }
        private ISceneVisibility? _currentVisibility;

        public bool ViewportWantsInput => ViewportPanel?.IsHovered ?? false;
        public ImFontPtr FaFont => _faFont;

        private AlertPanel? _alertPanel;

        private bool _firstLoop = true;
        private bool _xnoWindow = true;
        private bool _stageWindow = true;
        private bool _environmentWindow = true;
        private bool _fileBrowser = true;
        private bool _guizmos = true;
        private float _sunAzimuth;
        private float _sunAltitude;

        private ImFontPtr _faFont;

        public unsafe void OnLoad(ImGuiController controller, WebGPU wgpu, WgpuDevice device)
        {
            Controller = controller;
            _alertPanel = new AlertPanel();
            ObjectsPanel = new ObjectsPanel();
            StagesPanel = new StagesPanel(this);
            ViewportPanel = new ViewportPanel(wgpu, device, controller);

            var io = ImGui.GetIO();
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            io.ConfigDpiScaleFonts = true;

            var interBytes = EmbeddedResources.ReadAllBytes("XNOEdit/Fonts/Inter.ttf");
            var faBytes = EmbeddedResources.ReadAllBytes("XNOEdit/Fonts/FA-Solid.ttf");
            ushort[] faRanges = [FontAwesome7.IconMin, FontAwesome7.IconMax, 0];

            fixed (byte* pBytes = interBytes)
            fixed (byte* pFaBytes = faBytes)
            fixed (ushort* pFaRanges = faRanges)
            {
                var config = ImGui.ImFontConfig();
                config.FontDataOwnedByAtlas = false;

                var font = io.Fonts.AddFontFromMemoryTTF(pBytes, interBytes.Length, 15f, config);
                io.FontDefault = font;

                config.GlyphOffset = new Vector2(0, 1);
                config.MergeMode = true;
                config.PixelSnapH = true;

                _faFont = io.Fonts.AddFontFromMemoryTTF(pFaBytes, faBytes.Length, 15f, config, (uint*)pFaRanges);
            }
        }

        public void InitSunAngles(RenderSettings settings)
        {
            _sunAltitude = MathF.Asin(settings.SunDirection.Y) * 180.0f / MathF.PI;
            _sunAzimuth = MathF.Atan2(settings.SunDirection.Z, settings.SunDirection.X) * 180.0f / MathF.PI;

            if (_sunAzimuth < 0)
                _sunAzimuth += 360.0f;
        }

        public ObjectSceneVisibility InitXnoPanel(NinjaNext xno)
        {
            StagePanel = null;

            var visibility = new ObjectSceneVisibility();
            _currentVisibility = visibility;

            XnoPanel = new XnoPanel(xno, visibility);

            return visibility;
        }

        public StageSceneVisibility InitStagePanel(string name, List<NinjaNext> xnos)
        {
            XnoPanel = null;

            var visibility = new StageSceneVisibility();
            _currentVisibility = visibility;

            StagePanel = new StagePanel(name, xnos, visibility);
            StagePanel.ViewXno += (index, xno) =>
            {
                // Key change: shares same visibility instance with xnoIndex
                XnoPanel = new XnoPanel(xno, visibility, index);
                ImGui.SetWindowFocus("###XnoPanel");
            };

            return visibility;
        }

        public unsafe void OnRender(
            Matrix4x4 view, Matrix4x4 projection,
            double deltaTime, ref RenderSettings settings, RenderPassEncoder* pass, TextureManager textureManager)
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

                    ImGui.MenuItem("Guizmos", "", ref _guizmos);
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

                    if (ImGui.MenuItem("Reset Layout"))
                    {
                        _xnoWindow = true;
                        _stageWindow = true;
                        _environmentWindow = true;
                        _fileBrowser = true;
                        _firstLoop = true;
                    }

                    ImGui.Separator();

                    ImGui.MenuItem("XNO", "", ref _xnoWindow);
                    ImGui.MenuItem("Stage", "", ref _stageWindow);
                    ImGui.MenuItem("Environment", "", ref _environmentWindow);
                    ImGui.MenuItem("File Browser", "", ref _fileBrowser);

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
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.65f);
                ImGui.Text("Camera Sensitivity");
                ImGui.SliderFloat("##CameraSensitivity", ref settings.CameraSensitivity, 0.0f, 1.0f);
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

            if (_stageWindow)
                StagePanel?.Render();

            if (_fileBrowser)
            {
                ObjectsPanel?.Render();
                StagesPanel?.Render();
            }

            ViewportPanel?.Render(view, projection, _guizmos);

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
