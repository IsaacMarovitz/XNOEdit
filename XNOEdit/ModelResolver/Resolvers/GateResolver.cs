using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class GateResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "gate"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            // TODO: Place cage02
            var modelPath = "/win32/object/kdv/cage01/kdv_obj_cage01.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create(modelPath, setObject.Position, setObject.Rotation));
        }
    }
}
