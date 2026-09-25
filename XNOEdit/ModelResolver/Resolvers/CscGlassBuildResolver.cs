using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class CscGlassBuildResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "cscglassbuildbomb"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var modelPath = "/win32/object/csc/glassbuild/csc_obj_glassbuild.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create(modelPath, setObject.Position, setObject.Rotation));
        }
    }
}
