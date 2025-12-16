using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public abstract class ModelResolver : IModelResolver
    {
        protected abstract IReadOnlySet<string> SupportedTypes { get; }
        public virtual int Priority => 0;

        public bool CanResolve(string objectType) => SupportedTypes.Contains(objectType);
        public abstract string[] Resolve(Package package, StageSetObject setObject);
    }
}
