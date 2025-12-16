using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class GuillotineResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "common_guillotine"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            foreach (var group in ObjectPackagesMap.All)
            {
                foreach (var packageEntry in group.ObjectPackages)
                {
                    if (packageEntry.Key != setObject.Type)
                        continue;

                    var packagePath = $"/xenon/object/{group.Folder}/{packageEntry.Value}.pkg";
                    var package = context.LoadPackage(packagePath);
                    if (package == null)
                        continue;

                    var category = package.Categories.FirstOrDefault(x => x.Name == "model");
                    if (category == null)
                        return ResolveResult.Failed("Could not find model category in guillotine package");

                    var modelAFile = category.Files.FirstOrDefault(x => x.Name == "modelA");
                    var modelBFile = category.Files.FirstOrDefault(x => x.Name == "modelB");
                    var modelCFile = category.Files.FirstOrDefault(x => x.Name == "modelC");

                    var variant = setObject.Parameters.Count > 0 ? setObject.Parameters[0].Value : 0;

                    var modelPath = variant switch
                    {
                        1 => modelAFile?.Location,
                        2 => modelBFile?.Location,
                        3 => modelCFile?.Location,
                        _ => null
                    };

                    if (string.IsNullOrEmpty(modelPath))
                        return ResolveResult.Failed($"Could not find requested guillotine model {variant}");

                    return ResolveResult.WithInstance(
                        ResolvedInstance.Create($"/win32/{modelPath}", setObject.Position, setObject.Rotation));
                }
            }

            return ResolveResult.Failed($"Package not found for {setObject.Type}");
        }
    }
}
