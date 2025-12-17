using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class AqaMagnetResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "aqa_magnet"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var package = context.FindPackageForType(setObject.Type);
            if (package == null)
                return ResolveResult.Failed($"Package not found for {setObject.Type}");

            var category = package.Categories.FirstOrDefault(x => x.Name == "model");
            if (category == null)
                return ResolveResult.Failed("Could not find model category in magnet package");

            var variant = setObject.Parameters.Count > 0 ? setObject.Parameters[0].Value : 0;
            var modelPath = ResolverContext.GetVariantModel(category, (int)variant,
                "bofmodel", "bonmodel", "aofmodel", "ronmodel", "ronmodel");

            if (string.IsNullOrEmpty(modelPath))
                return ResolveResult.Failed($"Could not find requested magnet model {variant}");

            return ResolveResult.WithInstance(
                ResolvedInstance.Create($"/win32/{modelPath}", setObject.Position, setObject.Rotation));
        }
    }
}
