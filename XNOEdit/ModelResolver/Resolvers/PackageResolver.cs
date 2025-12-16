using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class PackageResolver : IModelResolver
    {
        public int Priority => -20;

        public bool CanResolve(string objectType) => true;

        public ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
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
                        return ResolveResult.Empty;

                    var modelFile = category.Files.FirstOrDefault(x => x.Name == "model");
                    if (modelFile == null)
                        return ResolveResult.Empty;

                    return ResolveResult.WithInstance(
                        ResolvedInstance.Create(modelFile.Location, setObject.Position, setObject.Rotation));
                }
            }

            return ResolveResult.Failed($"Package not found for {setObject.Type}");
        }
    }
}
