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

        public void Render(IReadOnlyDictionary<string, IntPtr> textures)
        {
            ImGui.Begin($"{_xno.Name}###XnoPanel", ImGuiWindowFlags.AlwaysAutoResize);

            if (ImGui.BeginTabBar("Tab Bar", ImGuiTabBarFlags.AutoSelectNewTabs))
            {
                // Texture
                var textureListChunk = _xno.GetChunk<TextureListChunk>();
                if (textureListChunk != null)
                {
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
                }


                // Effect
                var effectListChunk = _xno.GetChunk<EffectListChunk>();
                if (effectListChunk != null)
                {
                    if (ImGui.BeginTabItem("Effect List"))
                    {
                        var uniqueEffects = effectListChunk.Effects
                            .GroupBy(x => x.Name)
                            .Select(g => g.FirstOrDefault());

                        foreach (var effect in uniqueEffects)
                        {
                            if (ImGui.CollapsingHeader(effect.Name))
                            {
                                ImGui.Text("Techniques:");

                                foreach (var technique in effectListChunk.Techniques)
                                {
                                    if (technique.GetEffect(effectListChunk).Name == effect.Name)
                                    {
                                        ImGui.BulletText(technique.Name);
                                    }
                                }
                            }
                        }

                        ImGui.EndTabItem();
                    }
                }


                // Object
                var objectChunk = _xno.GetChunk<ObjectChunk>();
                if (objectChunk != null)
                {
                    if (ImGui.BeginTabItem("Object"))
                    {
                        var center = objectChunk.Centre;
                        ImGui.InputFloat3("Center", ref center, "%.1f", ImGuiInputTextFlags.ReadOnly);

                        if (objectChunk.BoundingBox.HasValue)
                        {
                            var boundingBox = objectChunk.BoundingBox.Value;
                            ImGui.InputFloat3("Bounding Box", ref boundingBox, "%.1f", ImGuiInputTextFlags.ReadOnly);
                        }

                        var radius = objectChunk.Radius;
                        ImGui.InputFloat("Radius", ref radius, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);

                        ImGui.Text($"Texture Count: {objectChunk.TextureCount}");
                        ImGui.Text($"Subobject Count: {objectChunk.SubObjects.Count}");

                        ImGui.SeparatorText("Materials");
                        for (var i = 0; i < objectChunk.Materials.Count; i++)
                        {
                            ImGui.PushID(i);
                            if (ImGui.CollapsingHeader($"Material {i + 1}"))
                            {
                                var material = objectChunk.Materials[i];

                                var ambient = new Vector4(material.Colour.Ambient.R, material.Colour.Ambient.G,
                                    material.Colour.Ambient.B, material.Colour.Ambient.A);

                                var diffuse = new Vector4(material.Colour.Diffuse.R, material.Colour.Diffuse.G,
                                    material.Colour.Diffuse.B, material.Colour.Diffuse.A);

                                var specular = new Vector4(material.Colour.Specular.R, material.Colour.Specular.G,
                                    material.Colour.Specular.B, material.Colour.Specular.A);

                                var emissive = new Vector4(material.Colour.Emissive.R, material.Colour.Emissive.G,
                                    material.Colour.Emissive.B, material.Colour.Emissive.A);

                                var power = material.Colour.Power;

                                ImGui.ColorEdit4("Ambient", ref ambient, ImGuiColorEditFlags.NoInputs);
                                ImGui.ColorEdit4("Diffuse", ref diffuse, ImGuiColorEditFlags.NoInputs);
                                ImGui.ColorEdit4("Specular", ref specular, ImGuiColorEditFlags.NoInputs);
                                ImGui.ColorEdit4("Emissive", ref emissive, ImGuiColorEditFlags.NoInputs);
                                ImGui.InputFloat("Power", ref power, 0f, 0f, "%.1f", ImGuiInputTextFlags.ReadOnly);
                            }
                            ImGui.PopID();
                        }

                        ImGui.EndTabItem();
                    }
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }
    }
}
