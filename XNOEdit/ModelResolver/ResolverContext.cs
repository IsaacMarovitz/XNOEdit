using Marathon.Formats.Archive;
using Marathon.Formats.Ninja;
using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public class ResolverContext
    {
        public ArcFile ObjectArchive { get; }
        public List<ObjectPhysicsParameter> ObjectParameters { get; }
        public Actor Actor { get; }

        private readonly Dictionary<string, NinjaNext> _xnoCache = new();
        private readonly Dictionary<string, Package> _packageCache = new();

        public ResolverContext(
            List<ObjectPhysicsParameter> objectParameters,
            Actor actor,
            ArcFile objectArchive)
        {
            ObjectParameters = objectParameters;
            Actor = actor;
            ObjectArchive = objectArchive;
        }

        public Package? LoadPackage(string path)
        {
            if (_packageCache.TryGetValue(path, out var cached))
                return cached;

            var file = ObjectArchive.GetFile(path);
            if (file == null)
                return null;

            var package = new Package(file.Decompress());
            _packageCache[path] = package;
            return package;
        }

        public NinjaNext? LoadXno(string path)
        {
            if (_xnoCache.TryGetValue(path, out var cached))
                return cached;

            var file = ObjectArchive.GetFile(path);
            if (file == null)
                return null;

            var xno = new NinjaNext(file.Decompress());
            _xnoCache[path] = xno;
            return xno;
        }

        public void ClearCaches()
        {
            _xnoCache.Clear();
            _packageCache.Clear();
        }
    }
}
