using System.Numerics;
using ImGuiNET;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;

namespace XNOEdit.Panels
{
    public class ImGuiXnoPanel
    {
        private readonly NinjaNext _xno;

        public ImGuiXnoPanel(NinjaNext xno)
        {
            _xno = xno;
        }

        public void Render(Dictionary<string, IntPtr> textures)
        {
            ImGui.Begin($"{_xno.Name}", ImGuiWindowFlags.AlwaysAutoResize);

            if (ImGui.BeginTabBar("Tab Bar", ImGuiTabBarFlags.AutoSelectNewTabs))
            {
                // Texture
                var textureListChunk = _xno.GetChunk<TextureListChunk>();
                if (ImGui.BeginTabItem("Texture List"))
                {
                    foreach (var texture in textureListChunk.Textures)
                    {
                        if (ImGui.CollapsingHeader(texture.Name))
                        {
                            ImGui.Text($"Bank: {texture.Bank}");
                            ImGui.Text($"Global Index: {texture.GlobalIndex}");
                            ImGui.Text($"Min Filter: {texture.MinFilter}");
                            ImGui.Text($"Mag Filter: {texture.MagFilter}");
                            ImGui.Text($"Type: {texture.Type}");

                            ImGui.Image(textures.First(x => x.Key == texture.Name).Value, new Vector2(150, 150));
                        }
                    }

                    ImGui.EndTabItem();
                }


                // Effect
                var effectListChunk = _xno.GetChunk<EffectListChunk>();
                if (ImGui.BeginTabItem("Effect List"))
                {
                    ImGui.SeparatorText("Effects");

                    foreach (var effect in effectListChunk.Effects)
                    {
                        if (ImGui.CollapsingHeader(effect.Name))
                        {
                            ImGui.Text($"Type: {effect.Type}");
                        }
                    }

                    ImGui.SeparatorText("Techniques");

                    foreach (var technique in effectListChunk.Techniques)
                    {
                        if (ImGui.CollapsingHeader(technique.Name))
                        {
                            ImGui.Text($"Type: {technique.Type}");
                            ImGui.Text($"Effect Index: {technique.EffectIndex}");
                        }
                    }
                    ImGui.EndTabItem();
                }


                // Object
                var objectChunk = _xno.GetChunk<ObjectChunk>();
                if (ImGui.BeginTabItem("Object"))
                {

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }
}
