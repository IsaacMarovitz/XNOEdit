using Marathon.Formats.Archive;
using Marathon.Formats.Ninja;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Types;
using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public class ResolverContext
    {
        public ArcFile ObjectArchive { get; }
        public List<ObjectPhysicsParameter> PhysicsParameters { get; }
        public List<PathObjParameter> PathParameters { get; }
        public List<Actor> Actors { get; }

        private readonly Dictionary<string, NinjaNext> _xnoCache = new();
        private readonly Dictionary<string, Package> _packageCache = new();

        public ResolverContext(
            List<ObjectPhysicsParameter> physicsParameters,
            List<PathObjParameter> pathParams,
            List<Actor> actors,
            ArcFile objectArchive)
        {
            PhysicsParameters = physicsParameters;
            PathParameters = pathParams;
            Actors = actors;
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

        public Package? FindPackageForType(string type)
        {
            return (from @group in ObjectPackagesMap.All
                    from packageEntry in @group.ObjectPackages
                    where packageEntry.Key == type
                    select $"/xenon/object/{@group.Folder}/{packageEntry.Value}.pkg"
                    into packagePath
                    select LoadPackage(packagePath))
                .FirstOrDefault();
        }

        public static Node FindNodeByName(ObjectChunk objectChunk, NodeNameChunk nameChunk, string name)
        {
            var indexOf = nameChunk.Names.Select((value, index) => new { value, index })
                .Where(pair => pair.value == name)
                .Select(pair => pair.index + 1)
                .FirstOrDefault() - 1;

            return objectChunk.Nodes[indexOf];
        }

        public void ClearCaches()
        {
            _xnoCache.Clear();
            _packageCache.Clear();
        }
    }
}
