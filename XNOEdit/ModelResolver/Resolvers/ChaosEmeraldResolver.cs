using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class ChaosEmeraldResolver : ModelResolver
    {
         protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "common_chaosemerald",
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var package = context.FindPackageForType(setObject.Type);
            if (package == null)
                return ResolveResult.Failed($"Package not found for {setObject.Type}");

            var category = package.Categories.FirstOrDefault(x => x.Name == "model");
            if (category == null)
                return ResolveResult.Failed("Could not find model category in chaos emerald package");

            var variant = setObject.Parameters.Count > 0 ? setObject.Parameters[0].Value : 0;
            var modelPath = ResolverContext.GetVariantModel(category, (int)variant,
                "model_w", "model_s", "model_y", "model_p", "model_g", "model_b", "model_r");

            if (string.IsNullOrEmpty(modelPath))
                return ResolveResult.Failed($"Could not find chaos emerald model for variant {variant}");

            return ResolveResult.WithInstance(
                ResolvedInstance.Create($"/win32/{modelPath}", setObject.Position, setObject.Rotation));
        }
    }
}
