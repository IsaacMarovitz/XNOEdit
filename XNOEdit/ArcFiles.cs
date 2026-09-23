using Marathon.Formats.Archive;

namespace XNOEdit
{
    public static class ArcFiles
    {
        public static ArcFile ScriptsArc => new(Path.Join(
            XenonArcFolder,
            "scripts.arc"));

        public static ArcFile EnemyArc => new(Path.Join(
            XenonArcFolder,
            "enemy.arc"));

        public static ArcFile HumanArc => new(Path.Join(
            XenonArcFolder,
            "human.arc"));

        public static ArcFile ObjectArc => new(Path.Join(
            XenonArcFolder,
            "object.arc"));

        public static ArcFile ShaderArc => new(Path.Join(
            XenonArcFolder,
            "shader.arc"));

        public static ArcFile GameArc => new(Path.Join(
            XenonArcFolder,
            "game.arc"));

        public static ArcFile TextArc = new(Path.Join(
            XenonArcFolder,
            "text.arc"));

        public static ArcFile Win32Arc(string name, bool withExtension = false)
        {
            return new ArcFile(Path.Join(
                Win32ArcFolder,
                withExtension ? name : $"{name}.arc"));
        }

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
