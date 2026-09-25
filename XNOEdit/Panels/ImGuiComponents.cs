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

        public static bool StyledCheckbox(string label, bool value)
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
                ImGui.PushStyleColor(ImGuiCol.Text, style.Colors[(int)ImGuiCol.CheckMark]);

            ImGui.PushStyleColor(ImGuiCol.Button, style.Colors[(int)ImGuiCol.FrameBg]);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, style.Colors[(int)ImGuiCol.FrameBgActive]);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, style.Colors[(int)ImGuiCol.FrameBgHovered]);

            var button = ImGui.Button($"{icon}{label}");

            ImGui.PopStyleColor(4);

            return button;
        }

        public static void RenderFilesListTabItem(string title, IEnumerable<File> files,
            Action<File> clickAction, string? searchText = null)
        {
            if (ImGui.BeginTabItem(title))
            {
                RenderFilesList(files, clickAction, searchText);

                ImGui.EndTabItem();
            }
        }

        public static void RenderFilesList(IEnumerable<File> files, Action<File> clickAction, string? searchText = null)
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
        }

        private const float LabelWidth = 100f;

        public static void SetNextItemFillWidth()
        {
            ImGui.SetNextItemWidth(-float.Epsilon);
        }

        private static string Label(string label)
        {
            // Get visible portion only (before ##)
            var hashIndex = label.IndexOf("##", StringComparison.Ordinal);
            var visibleLabel = hashIndex >= 0 ? label[..hashIndex] : label;

            if (visibleLabel.Length == 0)
                return label;

            var x = ImGui.GetCursorPosX();

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(visibleLabel);
            ImGui.SameLine(x + LabelWidth);
            SetNextItemFillWidth();

            return $"##{label}";
        }

        public static bool InputFloat(string label, ref float value, float step = 0f, float stepFast = 0f, string format = "%.3f", ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            return ImGui.InputFloat(Label(label), ref value, step, stepFast, format, flags);
        }

        public static bool InputFloat(string label, ref float value, string format, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            return ImGui.InputFloat(Label(label), ref value, format, flags);
        }

        public static bool InputFloat3(string label, ref Vector3 value, string format = "%.3f", ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            return ImGui.InputFloat3(Label(label), ref value, format, flags);
        }

        public static bool InputFloat4(string label, ref Vector4 value, string format = "%.3f", ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            return ImGui.InputFloat4(Label(label), ref value, format, flags);
        }

        public static bool ColorEdit4(string label, ref Vector4 color, ImGuiColorEditFlags flags = ImGuiColorEditFlags.None)
        {
            return ImGui.ColorEdit4(Label(label), ref color, flags);
        }

        public static bool ColorEdit3(string label, ref Vector3 color, ImGuiColorEditFlags flags = ImGuiColorEditFlags.None)
        {
            return ImGui.ColorEdit3(Label(label), ref color, flags);
        }

        public static bool SliderFloat(string label, ref float value, float min, float max, string format = "%.3f", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
        {
            return ImGui.SliderFloat(Label(label), ref value, min, max, format, flags);
        }

        public static bool DragFloat(string label, ref float value, float speed = 1f, float min = 0f, float max = 0f, string format = "%.3f", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
        {
            return ImGui.DragFloat(Label(label), ref value, speed, min, max, format, flags);
        }

        public static bool Checkbox(string label, ref bool value)
        {
            return ImGui.Checkbox(Label(label), ref value);
        }
    }
}
