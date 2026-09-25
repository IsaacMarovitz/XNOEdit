using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class WindSwitchResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "windswitch"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var modelPath = "/win32/object/kdv/windswitch/Wswitch.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create(modelPath, setObject.Position, setObject.Rotation));
        }
    }
}
