using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public interface IModelResolver
    {
        ResolveResult Resolve(ResolverContext context, StageSetObject setObject);
        int Priority => 0;
        bool CanResolve(string objectType);
    }

    public readonly struct ResolveResult
    {
        public IReadOnlyList<ResolvedInstance> Instances { get; init; }
        public bool Success { get; init; }
        public bool Skip { get; init; }
        public string? ErrorMessage { get; init; }

        public static ResolveResult Skipped => new() { Skip = true, Success = true, Instances = [] };

        public static ResolveResult Empty => new() { Success = true, Instances = [] };

        public static ResolveResult Failed(string message) => new()
        {
            Success = false,
            ErrorMessage = message,
            Instances = []
        };

        public static ResolveResult WithInstances(IReadOnlyList<ResolvedInstance> instances) => new()
        {
            Success = true,
            Instances = instances
        };

        public static ResolveResult WithInstance(ResolvedInstance instance) => new()
        {
            Success = true,
            Instances = [instance]
        };
    }
}
