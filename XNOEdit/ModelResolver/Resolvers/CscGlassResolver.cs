using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class CscGlassResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "cscglass"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var modelPath = "/win32/object/csc/glass/csc_obj_glass.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create(modelPath, setObject.Position, setObject.Rotation));
        }
    }
}
