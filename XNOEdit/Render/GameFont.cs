using System.Numerics;
using Marathon.Formats.Text;

namespace XNOEdit.Render
{
    public class GameFont
    {
        private const ushort MissingGlyph = 0xFFFF;

        private readonly TextFontMap _map;
        private readonly TextFontProportion _proportion;
        private readonly Vector2 _atlasSize;

        public string AtlasName { get; }
        public string Name => _map.Name;
        public Vector2 CellSize { get; }
        public float LineHeight => CellSize.Y;

        public GameFont(TextFontMap map, TextFontProportion proportion, Vector2 cellSize, Vector2 atlasSize, string atlasName)
        {
            _map = map;
            _proportion = proportion;
            _atlasSize = atlasSize;

            AtlasName = atlasName;
            CellSize = cellSize;
        }

        public bool TryGetGlyph(char character, out Vector2 uvMin, out Vector2 uvMax, out Vector2 size, out int advance)
        {
            uvMin = uvMax = size = default;
            advance = 0;

            var page = character >> 8;

            if (page >= _map.CodePages.Length || _map.CodePages[page] is not { } codePage)
                return false;

            var glyph = codePage.Characters[character & 0xFF];

            if (glyph.Flags == MissingGlyph)
                return false;

            var width = GetWidth(character);
            var origin = new Vector2(glyph.Column, glyph.Row) * CellSize;

            size = new Vector2(width, CellSize.Y);
            uvMin = origin / _atlasSize;
            uvMax = (origin + size) / _atlasSize;
            advance = width + _proportion.Kerning;

            return true;
        }

        private int GetWidth(char character)
        {
            var index = character - _proportion.CodePageSeek;

            if (index >= 0 && index < _proportion.Characters.Length && _proportion.Characters[index].Width != 0)
                return _proportion.Characters[index].Width;

            return _proportion.CellWidth;
        }

        public readonly record struct TextFontData(
            string TexturePath, int TextureWidth, int TextureHeight,
            string MapPath, string ProportionPath, int CellWidth, int CellHeight);

        // Data from font table at 0x82B7F860
        public static readonly TextFontData[] TextFonts =
        [
            new("text/font.dds",
                2048,
                2048,
                "text/fontmap.ftm",
                "text/pf20_ff.pfi",
                28,
                28),
            new("text/font2.dds",
                1024,
                512,
                "text/font2map.ftm",
                "text/pf20_ff2.pfi",
                37,
                50),
            new("text/font3.dds",
                512,
                512,
                "text/font3map.ftm",
                "text/pf20_ff3.pfi",
                31,
                41),
        ];
    }
}
