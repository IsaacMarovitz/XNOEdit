using System.Numerics;
using Hexa.NET.ImGui;
using XNOEdit.Fonts;

namespace XNOEdit.Panels
{
    public static class ImGuiComponents
    {
        public struct File
        {
            public string Name;
            public string Identifier;

            public File(string name, string identifier)
            {
                Name = name;
                Identifier = identifier;
            }
        }

        public static unsafe bool StyledCheckbox(string label, bool value)
        {
            var pos = ImGui.GetCursorScreenPos();
            var icon = value ? FontAwesome7.Eye : FontAwesome7.EyeSlash;

            // Get visible portion only (before ##)
            var hashIndex = label.IndexOf("##", StringComparison.Ordinal);
            var visibleLabel = hashIndex >= 0 ? label[..hashIndex] : label;
            var displayText = $"{icon}{visibleLabel}";

            var textSize = ImGui.CalcTextSize(displayText);
            var style = ImGui.GetStyle();
            var buttonSize = new Vector2(
                textSize.X + style.FramePadding.X * 2,
                textSize.Y + style.FramePadding.Y * 2
            );

            var hovered = ImGui.IsMouseHoveringRect(pos, pos + buttonSize);

            if (value && !hovered)
                ImGui.PushStyleColor(ImGuiCol.Text, Vector4.Zero);
            else
                ImGui.PushStyleColor(ImGuiCol.Text, *ImGui.GetStyleColorVec4(ImGuiCol.CheckMark));

            ImGui.PushStyleColor(ImGuiCol.Button, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBg));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgActive));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, *ImGui.GetStyleColorVec4(ImGuiCol.FrameBgHovered));

            var button = ImGui.Button($"{icon}{label}");

            ImGui.PopStyleColor(4);

            return button;
        }

        public static void RenderFilesList(string title, IEnumerable<File> files,
            Action<File> clickAction, string searchText = null)
        {
            if (ImGui.BeginTabItem(title))
            {
                // Filter files based on search text
                var filteredFiles = string.IsNullOrWhiteSpace(searchText)
                    ? files.ToList()
                    : files.Where(f => f.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

                // Grid settings
                var thumbnailSize = 80.0f;
                var padding = 8.0f;
                var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                var textHeight = lineHeight * 2; // Space for 2 lines
                var itemHeight = thumbnailSize + textHeight + padding * 2;

                // Render scrollable grid
                ImGui.BeginChild("FileGrid", Vector2.Zero, ImGuiChildFlags.None);

                var availWidth = ImGui.GetContentRegionAvail().X;
                var columns = Math.Max(1, (int)(availWidth / (thumbnailSize + padding)));

                // Calculate even spacing to fill remaining width
                var totalItemsWidth = columns * thumbnailSize;
                var remainingSpace = availWidth - totalItemsWidth;
                var gapSize = remainingSpace / (columns + 1);

                // Add top spacing to match left spacing
                var startY = ImGui.GetCursorPosY() + gapSize;

                for (var i = 0; i < filteredFiles.Count; i++)
                {
                    var file = filteredFiles[i];
                    var columnIndex = i % columns;
                    var rowIndex = i / columns;

                    // Calculate position for this item
                    var xPos = gapSize + columnIndex * (thumbnailSize + gapSize);
                    var yPos = startY + rowIndex * itemHeight;

                    ImGui.SetCursorPos(new Vector2(xPos, yPos));

                    ImGui.PushID(i);

                    ImGui.BeginGroup();

                    // Thumbnail placeholder (as a button for interaction)
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
                    var clicked = ImGui.Button("##thumb", new Vector2(thumbnailSize, thumbnailSize));
                    ImGui.PopStyleColor();

                    // Create a child region to clip text
                    ImGui.BeginChild($"##text{i}", new Vector2(thumbnailSize, textHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

                    var singleLineHeight = ImGui.GetTextLineHeight();
                    var maxTextHeight = singleLineHeight * 2;

                    var displayName = file.Name;
                    var wrappedSize = ImGui.CalcTextSize(displayName, thumbnailSize);

                    while (wrappedSize.Y > maxTextHeight && displayName.Length > 4)
                    {
                        displayName = displayName[..^4] + "...";
                        wrappedSize = ImGui.CalcTextSize(displayName, thumbnailSize);
                    }

                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + thumbnailSize);
                    ImGui.TextWrapped(displayName);
                    ImGui.PopTextWrapPos();

                    ImGui.EndChild();

                    ImGui.EndGroup();

                    // Tooltip with full path on hover
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(file.Name);
                    }

                    // Handle click
                    if (clicked || ImGui.IsItemClicked())
                    {
                        clickAction?.Invoke(file);
                    }

                    ImGui.PopID();
                }

                ImGui.EndChild();
                ImGui.EndTabItem();
            }
        }
    }
}
