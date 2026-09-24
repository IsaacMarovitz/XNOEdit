using System.Collections.Concurrent;
using Marathon.Formats.Archive;

namespace XNOEdit
{
    public static class ArcFiles
    {
        private static readonly ConcurrentDictionary<string, ArcFile> _cache = new();

        public static ArcFile ScriptsArc => Load(Path.Join(
            XenonArcFolder,
            "scripts.arc"));

        public static ArcFile EnemyArc => Load(Path.Join(
            XenonArcFolder,
            "enemy.arc"));

        public static ArcFile HumanArc => Load(Path.Join(
            XenonArcFolder,
            "human.arc"));

        public static ArcFile ObjectArc => Load(Path.Join(
            XenonArcFolder,
            "object.arc"));

        public static ArcFile ShaderArc => Load(Path.Join(
            XenonArcFolder,
            "shader.arc"));

        public static ArcFile GameArc => Load(Path.Join(
            XenonArcFolder,
            "game.arc"));

        public static ArcFile TextArc = Load(Path.Join(
            XenonArcFolder,
            "text.arc"));

        public static ArcFile Win32Arc(string name, bool withExtension = false)
        {
            return new ArcFile(Path.Join(
                Win32ArcFolder,
                withExtension ? name : $"{name}.arc"));
        }

        private static ArcFile Load(string path) => _cache.GetOrAdd(path, x => new ArcFile(x));

        public static string XenonArcFolder => Path.Join(
            Configuration.GameFolder,
            "xenon",
            "archives");

        public static string Win32ArcFolder => Path.Join(
            Configuration.GameFolder,
            "win32",
            "archives");
    }
}
