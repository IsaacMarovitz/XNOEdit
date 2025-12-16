using System.Numerics;
using Hexa.NET.ImGui;
using XNOEdit.Managers;

namespace XNOEdit.Panels
{
    public class EnvironmentPanel
    {
        public const string Name = "Environment";

        private float _sunAzimuth;
        private float _sunAltitude;
        private UIManager _uiManager;

        public EnvironmentPanel(UIManager uiManager)
        {
            _uiManager = uiManager;
            uiManager.SetColors(UIManager.DefaultHue);
        }

        public void InitSunAngles(RenderSettings settings)
        {
            _sunAltitude = MathF.Asin(settings.SunDirection.Y) * 180.0f / MathF.PI;
            _sunAzimuth = MathF.Atan2(settings.SunDirection.Z, settings.SunDirection.X) * 180.0f / MathF.PI;

            if (_sunAzimuth < 0)
                _sunAzimuth += 360.0f;
        }

        public void Render(ref RenderSettings settings)
        {
            ImGui.Begin(Name);
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

            ImGui.SeparatorText("UI");

            var hue = _uiManager.GetHue();
            var editedHue = ImGui.SliderFloat("Accent Hue", ref hue, 0.0f, 360.0f);

            if (editedHue)
            {
                _uiManager.SetColors(hue);
            }

            ImGui.End();
        }
    }
}
