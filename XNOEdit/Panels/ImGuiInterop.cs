using System.Numerics;
using Hexa.NET.ImGui;

namespace XNOEdit.Panels
{
    internal static unsafe class ImGuiInterop
    {
        private static readonly List<Array> FontAllocations = [];

        public static void Image(ulong textureId, Vector2 size)
        {
            ImGui.Image(new ImTextureRef(null, textureId), size);
        }

        public static void SetNextWindowClass(ImGuiWindowClass windowClass)
        {
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(&windowClass));
        }

        public static ImFontPtr AddFontFromMemoryTTF(
            ImFontAtlasPtr atlas, ReadOnlySpan<byte> data, float size, ImFontConfigPtr config,
            ReadOnlySpan<uint> ranges = default)
        {
            var pinnedData = Pin(data);
            var pinnedRanges = Pin(ranges);

            fixed (byte* dataPointer = pinnedData)
            fixed (uint* rangesPointer = pinnedRanges)
            {
                return atlas.AddFontFromMemoryTTF(dataPointer, pinnedData.Length, size, config, rangesPointer);
            }
        }

        private static T[] Pin<T>(ReadOnlySpan<T> source) where T : unmanaged
        {
            var pinned = GC.AllocateArray<T>(source.Length, pinned: true);
            source.CopyTo(pinned);
            FontAllocations.Add(pinned);
            return pinned;
        }

        public static uint DockBuilderSplitNode(ref uint remaining, ImGuiDir direction, float ratio)
        {
            uint opposite;
            var node = ImGuiP.DockBuilderSplitNode(remaining, direction, ratio, null, &opposite);
            remaining = opposite;
            return node;
        }
    }
}
