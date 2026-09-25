using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class EagleResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "eagle"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var modelPath = "/win32/object/kdv/eagle01/kdv_obj_eagle01.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create(modelPath, setObject.Position, setObject.Rotation));
        }
    }
}
