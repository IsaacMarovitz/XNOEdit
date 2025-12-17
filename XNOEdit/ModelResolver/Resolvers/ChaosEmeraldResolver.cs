using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using XNOEdit.Logging;

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
                        return ResolveResult.Failed("Could not find model category in chaos emerald package");

                    var bFile = category.Files.FirstOrDefault(x => x.Name == "model_b");
                    var gFile = category.Files.FirstOrDefault(x => x.Name == "model_g");
                    var pFile = category.Files.FirstOrDefault(x => x.Name == "model_p");
                    var rFile = category.Files.FirstOrDefault(x => x.Name == "model_r");
                    var sFile = category.Files.FirstOrDefault(x => x.Name == "model_s");
                    var wFile = category.Files.FirstOrDefault(x => x.Name == "model_w");
                    var yFile = category.Files.FirstOrDefault(x => x.Name == "model_y");

                    var variant = setObject.Parameters.Count > 0 ? setObject.Parameters[0].Value : 0;

                    var modelPath = variant switch
                    {
                        1 => wFile?.Location,
                        2 => sFile?.Location,
                        3 => yFile?.Location,
                        4 => pFile?.Location,
                        5 => gFile?.Location,
                        6 => bFile?.Location,
                        7 => rFile?.Location,
                        _ => null
                    };

                    if (string.IsNullOrEmpty(modelPath))
                        return ResolveResult.Failed($"Could not find requested chaos emerald model {variant}");

                    return ResolveResult.WithInstance(
                        ResolvedInstance.Create($"/win32/{modelPath}", setObject.Position, setObject.Rotation));
                }
            }

            return ResolveResult.Failed($"Package not found for {setObject.Type}");
        }
    }
}
