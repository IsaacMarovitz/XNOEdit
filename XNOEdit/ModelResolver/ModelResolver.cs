using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public abstract class ModelResolver : IModelResolver
    {
        protected abstract IReadOnlySet<string> SupportedTypes { get; }
        public virtual int Priority => 0;

        public bool CanResolve(string objectType) => SupportedTypes.Contains(objectType);
        public abstract ResolveResult Resolve(ResolverContext context, StageSetObject setObject);
    }
}
