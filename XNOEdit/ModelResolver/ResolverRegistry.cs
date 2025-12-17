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
            Register(new PhysicsObjectResolver());
            Register(new GuillotineResolver());
            Register(new RevolvingNetResolver());
            Register(new EnemyResolver());
            Register(new AqaMagnetResolver());
            Register(new ItemboxResolver());
            Register(new GizmoResolver());
        }

        public void Register(IModelResolver resolver)
        {
            _resolvers.Add(resolver);
            _resolvers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            foreach (var resolver in _resolvers)
            {
                if (!resolver.CanResolve(setObject.Type))
                    continue;

                var result = resolver.Resolve(context, setObject);

                if (result.Skip || result.Instances.Count > 0 || !result.Success)
                    return result;
            }

            return _fallbackResolver.Resolve(context, setObject);
        }
    }
}
