using System.Numerics;
using ImGuiNET;
using Marathon.Formats.Ninja;
using Silk.NET.WebGPU;
using XNOEdit.Panels;
using XNOEdit.Renderer;

namespace XNOEdit.Managers
{
    public class UIManager : IDisposable
    {
        public event Action ResetCameraAction;

        public ImGuiController Controller { get; private set; }
        private ImGuiXnoPanel _xnoPanel;
        private ImGuiAlertPanel _alertPanel;

        private bool _xnoWindow = true;
        private bool _environmentWindow = true;
        private float _sunAzimuth;
        private float _sunAltitude;

        public void OnLoad(ImGuiController controller)
        {
            Controller = controller;
            _alertPanel = new ImGuiAlertPanel();

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        }

        public void InitSunAngles(RenderSettings settings)
        {
            _sunAltitude = MathF.Asin(settings.SunDirection.Y) * 180.0f / MathF.PI;
            _sunAzimuth = MathF.Atan2(settings.SunDirection.Z, settings.SunDirection.X) * 180.0f / MathF.PI;

            if (_sunAzimuth < 0)
                _sunAzimuth += 360.0f;
        }

        public void InitXnoPanel(NinjaNext xno)
        {
            _xnoPanel = new ImGuiXnoPanel(xno);
        }

        public unsafe void OnRender(double deltaTime, ref RenderSettings settings, RenderPassEncoder* pass, IReadOnlyDictionary<string, IntPtr> textures)
        {
            Controller.Update((float)deltaTime);

            ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode | ImGuiDockNodeFlags.NoDockingOverCentralNode);

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("Render"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("Show Grid", "G", ref settings.ShowGrid);
                    ImGui.MenuItem("Vertex Colors", "V", ref settings.VertexColors);
                    ImGui.MenuItem("Backface Culling", "C", ref settings.BackfaceCulling);
                    ImGui.MenuItem("Wireframe", "F", ref settings.WireframeMode);

                    ImGui.Separator();

                    if (ImGui.MenuItem("Reset Camera", "R"))
                    {
                        ResetCameraAction?.Invoke();
                    }

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    ImGui.PushItemFlag(ImGuiItemFlags.AutoClosePopups, false);

                    ImGui.MenuItem("XNO", null, ref _xnoWindow);
                    ImGui.MenuItem("Environment", null, ref _environmentWindow);

                    ImGui.PopItemFlag();
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            if (_environmentWindow)
            {
                ImGui.Begin("Environment");
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

            if (_xnoPanel != null)
            {
                if (_xnoWindow)
                    _xnoPanel.Render(textures);
            }
            else
            {
                ImGui.Begin("Help", ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.Text("Drag and drop a .xno file");
                ImGui.End();
            }

            _alertPanel.Render(deltaTime);
            Controller.Render(pass);
        }

        public void Dispose()
        {
            Controller?.Dispose();
        }

        public void TriggerAlert(AlertLevel alertLevel, string alert)
        {
            if (alert != string.Empty)
                _alertPanel.TriggerAlert(alertLevel, alert);
        }
    }
}
