using System.Numerics;
using Hexa.NET.ImGui;
using XNOEdit.Managers;
using XNOEdit.Render;

namespace XNOEdit.Panels
{
    public class EnvironmentPanel
    {
        public const string Name = "Environment";

        private UIManager _uiManager;

        public EnvironmentPanel(UIManager uiManager)
        {
            _uiManager = uiManager;
            uiManager.SetColors(UIManager.DefaultHue);
        }

        public void Render(RenderSettings settings, SceneEnvironment environment)
        {
            ImGui.Begin(Name);
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X * 0.65f);

            ImGui.Text("Camera Sensitivity");
            ImGuiComponents.SliderFloat("##CameraSensitivity", ref settings.CameraSensitivity, 0.0f, 1.0f);

            var config = environment.Config;
            var edited = false;

            ImGui.SeparatorText("Environment");
            ImGui.TextUnformatted($"Source: {(environment.IsOverridden ? "Scene" : "Default")}");
            ImGui.TextUnformatted($"Env Map: {environment.EnvMapName}");

            if (ImGui.Button("Reset"))
                environment.Reset();

            ImGui.SeparatorText("Ambient");
            edited |= EditColor("Ambient", config.Ambient, 0.01f, out var ambient);

            ImGui.SeparatorText("Main Light (Sun)");
            edited |= EditLight("Main", config.Main, out var main);

            ImGui.SeparatorText("Sub Light");
            edited |= EditLight("Sub", config.Sub, out var sub);

            ImGui.SeparatorText("Scattering");
            edited |= EditColor("Sun Color", config.Ols.SunColor, 0.1f, out var sunColor);
            edited |= EditColor("Rayleigh", config.Ols.BRay, 0.00001f, out var bRay);
            edited |= EditColor("Mie", config.Ols.BMie, 0.00001f, out var bMie);

            var g = config.Ols.G;
            edited |= ImGuiComponents.SliderFloat("Anisotropy", ref g, 0.0f, 0.999f);

            if (edited)
            {
                environment.Config = config with
                {
                    Ambient = ambient,
                    Main = main,
                    Sub = sub,
                    Ols = new SceneOls(sunColor, bRay, bMie, g),
                };
            }

            ImGui.SeparatorText("UI");

            var hue = _uiManager.GetHue();
            var editedHue = ImGuiComponents.SliderFloat("Accent Hue", ref hue, 0.0f, 360.0f);

            if (editedHue)
            {
                _uiManager.SetColors(hue);
            }

            ImGui.End();
        }

        private static bool EditColor(string label, Vector4 value, float speed, out Vector4 result)
        {
            var color = new Vector3(value.X, value.Y, value.Z);
            var intensity = value.W;
            var spacing = ImGui.GetStyle().ItemInnerSpacing.X;

            ImGui.PushID(label);
            var edited = ImGuiComponents.ColorEdit3(label, ref color, ImGuiColorEditFlags.NoInputs);
            ImGui.SameLine(0.0f, spacing);
            ImGuiComponents.SetNextItemFillWidth();
            edited |= ImGuiComponents.DragFloat("##Intensity", ref intensity, speed, 0.0f, 1000.0f, "%.4g");
            ImGui.PopID();

            result = new Vector4(color, intensity);
            return edited;
        }

        private static bool EditLight(string label, SceneLight light, out SceneLight result)
        {
            var edited = EditColor(label, light.Color, 0.01f, out var color);

            var direction = light.Direction;
            var azimuth = MathF.Atan2(direction.Z, direction.X) * 180.0f / MathF.PI;
            var altitude = MathF.Asin(Math.Clamp(direction.Y, -1.0f, 1.0f)) * 180.0f / MathF.PI;

            if (azimuth < 0)
                azimuth += 360.0f;

            ImGui.PushID(label);
            var editedAzimuth = ImGuiComponents.SliderFloat("Azimuth", ref azimuth, 0.0f, 360.0f, "%.1f°");
            var editedAltitude = ImGuiComponents.SliderFloat("Altitude", ref altitude, -89.0f, 89.0f, "%.1f°");
            ImGui.PopID();

            result = light with { Color = color };

            if (editedAzimuth || editedAltitude)
            {
                var azimuthRad = azimuth * MathF.PI / 180.0f;
                var altitudeRad = altitude * MathF.PI / 180.0f;

                result = result.WithDirection(new Vector3(
                    MathF.Cos(altitudeRad) * MathF.Cos(azimuthRad),
                    MathF.Sin(altitudeRad),
                    MathF.Cos(altitudeRad) * MathF.Sin(azimuthRad)));
            }

            return edited || editedAzimuth || editedAltitude;
        }
    }
}
