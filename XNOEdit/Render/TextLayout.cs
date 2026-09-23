using System.Numerics;

namespace XNOEdit.Render
{
    public readonly record struct TextQuad(Vector2 Position, Vector2 Size, Vector2 UvMin, Vector2 UvMax, int Advance);

    public static class TextLayout
    {
        private const char LineBreak = '\n';
        private const char PageBreak = '\f';

        public static List<List<TextQuad>> Build(GameFont font, string text)
        {
            var pages = new List<List<TextQuad>> { new() };
            var pen = Vector2.Zero;

            foreach (var character in text)
            {
                switch (character)
                {
                    case LineBreak:
                        pen = new Vector2(0, pen.Y + font.LineHeight);
                        continue;
                    case PageBreak:
                        pages.Add([]);
                        pen = Vector2.Zero;
                        continue;
                }

                if (!font.TryGetGlyph(character, out var uvMin, out var uvMax, out var size, out var advance))
                    continue;

                pages[^1].Add(new TextQuad(pen, size, uvMin, uvMax, advance));
                pen.X += advance;
            }

            return pages;
        }
    }
}
