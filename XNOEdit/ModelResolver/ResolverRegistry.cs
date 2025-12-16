using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;
using XNOEdit.ModelResolver.Resolvers;

namespace XNOEdit.ModelResolver
{
    public class ResolverRegistry
    {
        private readonly List<IModelResolver> _resolvers = [];
        private readonly PackageResolver _fallbackResolver = new();

        public ResolverRegistry()
        {
            Register(new GuillotineResolver());
            Register(new RevolvingNetResolver());
        }

        public void Register(IModelResolver resolver)
        {
            _resolvers.Add(resolver);
            _resolvers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public string[] Resolve(Package package, StageSetObject setObject)
        {
            foreach (var resolver in _resolvers)
            {
                if (!resolver.CanResolve(setObject.Type))
                    continue;

                var result = resolver.Resolve(package, setObject);

                if (result.Length > 0)
                    return result;
            }

            return _fallbackResolver.Resolve(package, setObject);
        }
    }
}
