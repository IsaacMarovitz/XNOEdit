using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class PsiMarkResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "common_psimarksphere"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var modelPath = "/win32/object/Common/esp_mark/cmn_espmark.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create(modelPath, setObject.Position, setObject.Rotation));
        }
    }
}
