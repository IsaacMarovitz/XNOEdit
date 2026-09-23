using System.Numerics;
using Hexa.NET.ImGui;
using XNOEdit.Managers;
using XNOEdit.Render;

namespace XNOEdit.Panels
{
    public static class TextPreview
    {
        public static GameFont? SelectFont(IReadOnlyList<GameFont> fonts, ref int index)
        {
            if (fonts.Count == 0)
            {
                ImGui.TextUnformatted("Text font not loaded");
                return null;
            }

            index = Math.Min(index, fonts.Count - 1);

            if (ImGui.BeginCombo("Font", fonts[index].Name))
            {
                for (var i = 0; i < fonts.Count; i++)
                {
                    if (ImGui.Selectable(fonts[i].Name, i == index))
                        index = i;
                }

                ImGui.EndCombo();
            }

            return fonts[index];
        }

        public static void Render(GameFont font, TextureManager textures, string text, bool outline, ref int page)
        {
            var pages = TextLayout.Build(font, text);

            page = Math.Min(page, pages.Count - 1);

            if (pages.Count > 1)
            {
                var number = page + 1;
                ImGui.SliderInt("Page", ref number, 1, pages.Count);
                page = number - 1;
            }

            var cell = font.CellSize;
            var extent = pages[page].Aggregate(cell, (size, quad) => Vector2.Max(size, quad.Position + quad.Size));

            var origin = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            var atlas = textures.GetImGuiTextureId(font.AtlasName);
            var outlineColor = ImGui.GetColorU32(ImGuiCol.Border);

            drawList.AddRectFilled(origin, origin + extent, ImGui.GetColorU32(ImGuiCol.FrameBg));

            foreach (var quad in pages[page])
            {
                var min = origin + quad.Position;
                ImGuiInterop.AddImage(drawList, atlas, min, min + quad.Size, quad.UvMin, quad.UvMax);
                if (outline)
                    drawList.AddRect(min, min + new Vector2(quad.Advance, cell.Y), outlineColor, 0.0f, ImDrawFlags.None, 1.0f);
            }

            ImGui.Dummy(extent);
        }
    }
}
