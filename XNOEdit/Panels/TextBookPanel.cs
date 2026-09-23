using System.Numerics;
using Hexa.NET.ImGui;
using Marathon.Formats.Text;
using XNOEdit.Managers;
using XNOEdit.Render;

namespace XNOEdit.Panels
{
    public class TextBookPanel
    {
        public const string Name = "Text Book";

        private readonly TextBook _book;

        private string _searchText = "";
        private TextCard? _selected;
        private int _page;
        private int _font;

        public TextBookPanel(TextBook book)
        {
            _book = book;
        }

        private bool Matches(TextCard card)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return true;

            return card.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                   card.Text.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        }

        public void Render(IReadOnlyList<GameFont> fonts, TextureManager textures)
        {
            ImGui.Begin(Name);

            ImGui.TextUnformatted($"Name: {_book.Name}");
            ImGui.TextUnformatted($"Card Count: {_book.Cards.Count}");

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextWithHint("##search", "Search", ref _searchText, 256);

            var flags = ImGuiTableFlags.RowBg |
                        ImGuiTableFlags.Borders |
                        ImGuiTableFlags.Resizable |
                        ImGuiTableFlags.ScrollY;

            if (ImGui.BeginTable("##cards", 3, flags, new Vector2(0, ImGui.GetContentRegionAvail().Y * 0.5f)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Variables", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                foreach (var card in _book.Cards)
                {
                    if (!Matches(card))
                        continue;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (ImGui.Selectable(card.Name, card == _selected, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _selected = card;
                        _page = 0;
                    }

                    ImGui.TableNextColumn();
                    ImGui.PushTextWrapPos(0.0f);
                    ImGui.TextUnformatted(card.Text);
                    ImGui.PopTextWrapPos();

                    ImGui.TableNextColumn();

                    if (card.Variables != null)
                        ImGui.TextUnformatted(string.Join(", ", card.Variables));
                }

                ImGui.EndTable();
            }

            if (_selected != null)
                RenderPreview(fonts, textures, _selected);

            ImGui.End();
        }

        private void RenderPreview(IReadOnlyList<GameFont> fonts, TextureManager textures, TextCard card)
        {
            ImGui.SeparatorText("Preview");

            // Books don't record which font a UI draws them in.
            if (TextPreview.SelectFont(fonts, ref _font) is { } font)
                TextPreview.Render(font, textures, card.Text, false, ref _page);
        }
    }
}
