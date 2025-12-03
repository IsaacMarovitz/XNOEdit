using Hexa.NET.ImGui;

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
                ImGui.BeginChild("FileGrid", System.Numerics.Vector2.Zero, ImGuiChildFlags.None);

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

                    ImGui.SetCursorPos(new System.Numerics.Vector2(xPos, yPos));

                    ImGui.PushID(i);

                    ImGui.BeginGroup();

                    // Thumbnail placeholder (as a button for interaction)
                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 1.0f));
                    var clicked = ImGui.Button("##thumb", new System.Numerics.Vector2(thumbnailSize, thumbnailSize));
                    ImGui.PopStyleColor();

                    // Create a child region to clip text
                    ImGui.BeginChild($"##text{i}", new System.Numerics.Vector2(thumbnailSize, textHeight), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);

                    // Estimate max characters for 2 lines
                    var avgCharWidth = ImGui.CalcTextSize("M").X;
                    var maxChars = (int)((thumbnailSize / avgCharWidth) * 2) - 3;

                    var displayName = file.Name;
                    if (displayName.Length > maxChars)
                    {
                        displayName = displayName[..maxChars] + "...";
                    }

                    // Center and wrap text
                    ImGui.PushTextWrapPos(thumbnailSize);

                    // Calculate wrapped text size
                    var wrappedSize = ImGui.CalcTextSize(displayName, thumbnailSize);

                    // Center horizontally only if it fits on one line
                    if (wrappedSize.X <= thumbnailSize)
                    {
                        var offset = (thumbnailSize - wrappedSize.X) * 0.5f;
                        ImGui.SetCursorPosX(offset);
                    }

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
