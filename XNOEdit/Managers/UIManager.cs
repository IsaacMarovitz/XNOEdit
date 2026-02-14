using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Ninja;
using Silk.NET.WebGPU;
using Solaris.Wgpu;
using XNOEdit.Fonts;
using XNOEdit.Logging;
using XNOEdit.Panels;
using XNOEdit.Renderer;
using XNOEdit.Renderer.Renderers;
using XNOEdit.Services;

namespace XNOEdit.Managers
{
    public class UIManager : IDisposable
    {
        public const float DefaultHue = 248.8f;
        public event Action ResetCameraAction;

        public ViewportPanel? ViewportPanel { get; private set; }
        public EnvironmentPanel? EnvironmentPanel { get; private set; }
        public ImGuiController? Controller { get; private set; }
        public ObjectsPanel? ObjectsPanel { get; private set; }
        public XnoPanel? XnoPanel { get; private set; }
        public MissionPanel? MissionPanel { get; private set; }
        public StagePanel? StagePanel { get; private set; }
        public StagesPanel? StagesPanel { get; private set; }
        public MissionsPanel? MissionsPanel { get; private set; }
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
        private float _hue;

        private ImFontPtr _faFont;

        public unsafe void OnLoad(ImGuiController controller, WebGPU wgpu, WgpuDevice device)
        {
            Controller = controller;
            _alertPanel = new AlertPanel();
            ObjectsPanel = new ObjectsPanel();
            StagesPanel = new StagesPanel(this);
            MissionsPanel = new MissionsPanel();
            ViewportPanel = new ViewportPanel(wgpu, device, controller);
            EnvironmentPanel = new EnvironmentPanel(this);

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

            var style = ImGui.GetStyle();
            style.FrameRounding = 3.0f;
            style.PopupRounding = 3.0f;
            style.ScrollbarRounding = 3.0f;
            style.TabRounding = 3.0f;
            style.ChildRounding = 3.0f;
            style.WindowRounding = 3.0f;
            style.GrabRounding = 2.0f;
            style.WindowBorderSize = 0.0f;
            style.PopupBorderSize = 0.0f;
            style.ChildBorderSize = 0.0f;
            style.FrameBorderSize = 0.0f;
            style.ImageBorderSize = 0.0f;
            style.SeparatorTextBorderSize = 1.0f;
            style.TabBarBorderSize = 1.0f;
        }

        public float GetHue()
        {
            return _hue;
        }

        public void SetColors(float hue)
        {
            _hue = hue;

            var colors = ImGui.GetStyle().Colors;
            colors[(int)ImGuiCol.Border] = OklchToRgba(0.543f, 0.0276f, _hue, 0.50f);
            colors[(int)ImGuiCol.FrameBg] = OklchToRgba(0.409f, 0.0906f, _hue, 0.54f);
            colors[(int)ImGuiCol.FrameBgHovered] = OklchToRgba(0.671f, 0.1685f, _hue, 0.40f);
            colors[(int)ImGuiCol.FrameBgActive] = OklchToRgba(0.671f, 0.1685f, _hue, 0.67f);
            colors[(int)ImGuiCol.TitleBgActive] = OklchToRgba(0.409f, 0.0906f, _hue, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = OklchToRgba(0.671f, 0.1685f, _hue, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = OklchToRgba(0.615f, 0.1566f, _hue, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = OklchToRgba(0.671f, 0.1685f, _hue, 1.00f);
            colors[(int)ImGuiCol.Button] = OklchToRgba(0.671f, 0.1685f, _hue, 0.40f);
            colors[(int)ImGuiCol.ButtonHovered] = OklchToRgba(0.671f, 0.1685f, _hue, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = OklchToRgba(0.628f, 0.1953f, _hue, 1.00f);
            colors[(int)ImGuiCol.Header] = OklchToRgba(0.671f, 0.1685f, _hue, 0.31f);
            colors[(int)ImGuiCol.HeaderHovered] = OklchToRgba(0.671f, 0.1685f, _hue, 0.80f);
            colors[(int)ImGuiCol.HeaderActive] = OklchToRgba(0.671f, 0.1685f, _hue, 1.00f);
            colors[(int)ImGuiCol.Separator] = OklchToRgba(0.543f, 0.0276f, _hue, 0.50f);
            colors[(int)ImGuiCol.SeparatorHovered] = OklchToRgba(0.516f, 0.1573f, _hue, 0.78f);
            colors[(int)ImGuiCol.SeparatorActive] = OklchToRgba(0.516f, 0.1573f, _hue, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip] = OklchToRgba(0.671f, 0.1685f, _hue, 0.20f);
            colors[(int)ImGuiCol.ResizeGripHovered] = OklchToRgba(0.671f, 0.1685f, _hue, 0.67f);
            colors[(int)ImGuiCol.ResizeGripActive] = OklchToRgba(0.671f, 0.1685f, _hue, 0.95f);
            colors[(int)ImGuiCol.TabHovered] = OklchToRgba(0.671f, 0.1685f, _hue, 0.80f);
            colors[(int)ImGuiCol.Tab] = OklchToRgba(0.464f, 0.1073f, _hue, 0.86f);
            colors[(int)ImGuiCol.TabSelected] = OklchToRgba(0.517f, 0.1234f, _hue, 1.00f);
            colors[(int)ImGuiCol.TabSelectedOverline] = OklchToRgba(0.671f, 0.1685f, _hue, 1.00f);
            colors[(int)ImGuiCol.TabDimmed] = OklchToRgba(0.215f, 0.0278f, _hue, 0.97f);
            colors[(int)ImGuiCol.TabDimmedSelected] = OklchToRgba(0.378f, 0.0793f, _hue, 1.00f);
            colors[(int)ImGuiCol.DockingPreview] = OklchToRgba(0.671f, 0.1685f, _hue, 0.70f);
            colors[(int)ImGuiCol.PlotLinesHovered] = OklchToRgba(0.712f, 0.1814f, _hue, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram] = OklchToRgba(0.789f, 0.1614f, _hue, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered] = OklchToRgba(0.772f, 0.1738f, _hue, 1.00f);
            colors[(int)ImGuiCol.TableHeaderBg] = OklchToRgba(0.312f, 0.0045f, _hue, 1.00f);
            colors[(int)ImGuiCol.TableBorderStrong] = OklchToRgba(0.432f, 0.0167f, _hue, 1.00f);
            colors[(int)ImGuiCol.TableBorderLight] = OklchToRgba(0.353f, 0.0087f, _hue, 1.00f);
            colors[(int)ImGuiCol.TextLink] = OklchToRgba(0.671f, 0.1685f, _hue, 1.00f);
            colors[(int)ImGuiCol.TextSelectedBg] = OklchToRgba(0.671f, 0.1685f, _hue, 0.35f);
            colors[(int)ImGuiCol.TreeLines] = OklchToRgba(0.543f, 0.0276f, _hue, 0.50f);
            colors[(int)ImGuiCol.DragDropTarget] = OklchToRgba(0.968f, 0.2110f, _hue, 0.90f);
            colors[(int)ImGuiCol.NavCursor] = OklchToRgba(0.671f, 0.1685f, _hue, 1.00f);
        }

        private static Vector4 OklchToRgba(float l, float c, float h, float a)
        {
            if (Math.Abs(h - 360.0f) < 0.001f)
                c = 0.0f;

            // OKLCH -> OKLAB
            var hRad = h * MathF.PI / 180f;
            var labA = c * MathF.Cos(hRad);
            var labB = c * MathF.Sin(hRad);

            // OKLAB -> linear RGB
            var l_ = l + 0.3963377774f * labA + 0.2158037573f * labB;
            var m_ = l - 0.1055613458f * labA - 0.0638541728f * labB;
            var s_ = l - 0.0894841775f * labA - 1.2914855480f * labB;

            var l3 = l_ * l_ * l_;
            var m3 = m_ * m_ * m_;
            var s3 = s_ * s_ * s_;

            var r = +4.0767416621f * l3 - 3.3077115913f * m3 + 0.2309699292f * s3;
            var g = -1.2684380046f * l3 + 2.6097574011f * m3 - 0.3413193965f * s3;
            var b = -0.0041960863f * l3 - 0.7034186147f * m3 + 1.7076147010f * s3;

            // Linear -> sRGB gamma
            r = r <= 0.0031308f ? 12.92f * r : 1.055f * MathF.Pow(r, 1f / 2.4f) - 0.055f;
            g = g <= 0.0031308f ? 12.92f * g : 1.055f * MathF.Pow(g, 1f / 2.4f) - 0.055f;
            b = b <= 0.0031308f ? 12.92f * b : 1.055f * MathF.Pow(b, 1f / 2.4f) - 0.055f;

            return new Vector4(
                Math.Clamp(r, 0f, 1f),
                Math.Clamp(g, 0f, 1f),
                Math.Clamp(b, 0f, 1f),
                a
            );
        }

        private static float HueForCategory(MissionCategory category)
        {
            return category switch
            {
                MissionCategory.Sonic => 240.0f,
                MissionCategory.Shadow => 0.0f,
                MissionCategory.Silver => 190.0f,
                MissionCategory.Eotw => 300.0f,
                MissionCategory.Solaris => 90.0f,
                _ => DefaultHue
            };
        }

        public ObjectSceneVisibility InitXnoPanel(NinjaNext xno, ModelRenderer renderer)
        {
            StagePanel = null;
            MissionPanel = null;

            var visibility = new ObjectSceneVisibility(renderer);
            _currentVisibility = visibility;

            XnoPanel = new XnoPanel(xno, visibility);
            ImGui.SetWindowFocus(XnoPanel.Name);
            SetColors(HueForCategory(MissionCategory.None));

            return visibility;
        }

        public StageSceneVisibility InitStagePanel(string name, List<NinjaNext> xnos, List<ModelRenderer> renderers)
        {
            XnoPanel = null;
            MissionPanel = null;

            var visibility = new StageSceneVisibility(renderers);
            _currentVisibility = visibility;

            StagePanel = new StagePanel(name, xnos, visibility);
            StagePanel.ViewXno += (index, xno) =>
            {
                XnoPanel = new XnoPanel(xno, visibility, index);
                ImGui.SetWindowFocus(XnoPanel.Name);
            };

            ImGui.SetWindowFocus(StagePanel.Name);
            SetColors(HueForCategory(MissionCategory.None));

            return visibility;
        }

        public void InitMissionPanel(string name)
        {
            MissionPanel = null;

            MissionPanel = new MissionPanel(name);

            ImGui.SetWindowFocus(MissionPanel.Name);
            var category = MissionsMap.GetMissionCategory(Path.GetFileNameWithoutExtension(name));
            SetColors(HueForCategory(category));
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

                ImGuiP.DockBuilderDockWindow(ViewportPanel.Name, remainingId);
                ImGuiP.DockBuilderDockWindow(EnvironmentPanel.Name, leftDock);
                ImGuiP.DockBuilderDockWindow(XnoPanel.Name, leftDock);
                ImGuiP.DockBuilderDockWindow(StagePanel.Name, leftDock);
                ImGuiP.DockBuilderDockWindow(MissionPanel.Name, leftDock);
                ImGuiP.DockBuilderDockWindow(ObjectsPanel.Name, bottomDock);
                ImGuiP.DockBuilderDockWindow(StagesPanel.Name, bottomDock);
                ImGuiP.DockBuilderDockWindow(MissionsPanel.Name, bottomDock);

                ImGuiP.DockBuilderFinish(dockspaceId);

                _firstLoop = false;
            }

            RenderMenuBar(ref settings);

            if (_environmentWindow)
                EnvironmentPanel?.Render(ref settings);

            if (_xnoWindow)
                XnoPanel?.Render(textureManager);

            if (_stageWindow)
                StagePanel?.Render();

            MissionPanel?.Render();

            if (_fileBrowser)
            {
                ObjectsPanel?.Render();
                StagesPanel?.Render();
                MissionsPanel?.Render();
            }

            ViewportPanel?.Render(view, projection, _guizmos);

            RenderLoadingOverlay();
            _alertPanel?.Render(deltaTime);
            Controller?.Render(pass);
        }

        private void RenderMenuBar(ref RenderSettings settings)
        {
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
            MissionsPanel?.LoadGameFolderResources();
        }
    }
}
