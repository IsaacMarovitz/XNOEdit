using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class ItemboxResolver : ModelResolver
    {
         protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "itemboxg",
            "itemboxa",
            "itembox_next"
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
                        return ResolveResult.Failed("Could not find model category in itembox package");

                    var groundBaseFile = category.Files.FirstOrDefault(x => x.Name == "model_ground_base");
                    var groundFile = category.Files.FirstOrDefault(x => x.Name == "model_ground");
                    var airFile = category.Files.FirstOrDefault(x => x.Name == "model_air");
                    var extendFile = category.Files.FirstOrDefault(x => x.Name == "model_extend");
                    var barrierFile = category.Files.FirstOrDefault(x => x.Name == "model_barrier");
                    var gaugeUpFile = category.Files.FirstOrDefault(x => x.Name == "model_gaugeup");
                    var invincibleFile = category.Files.FirstOrDefault(x => x.Name == "model_invincible");
                    var ring10File = category.Files.FirstOrDefault(x => x.Name == "model_ring10");
                    var ring20File = category.Files.FirstOrDefault(x => x.Name == "model_ring20");
                    var ring5File = category.Files.FirstOrDefault(x => x.Name == "model_ring5");
                    var speedupFile = category.Files.FirstOrDefault(x => x.Name == "model_speedup");

                    var variant = setObject.Parameters.Count > 0 ? setObject.Parameters[0].Value : 0;

                    var modelPath = variant switch
                    {
                        1 => ring5File?.Location,
                        2 => ring10File?.Location,
                        3 => ring20File?.Location,
                        4 => extendFile?.Location,
                        5 => speedupFile?.Location,
                        6 => gaugeUpFile?.Location,
                        7 => invincibleFile?.Location,
                        8 => barrierFile?.Location,
                        _ => null
                    };

                    if (string.IsNullOrEmpty(modelPath))
                        return ResolveResult.Failed($"Could not find requested itembox model {variant}");

                    return ResolveResult.WithInstance(
                        ResolvedInstance.Create($"/win32/{modelPath}", setObject.Position, setObject.Rotation));
                }
            }

            return ResolveResult.Failed($"Package not found for {setObject.Type}");
        }
    }
}
