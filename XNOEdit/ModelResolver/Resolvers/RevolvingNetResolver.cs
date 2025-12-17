using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using XNOEdit.Logging;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class RevolvingNetResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "wvo_revolvingnet"
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
                        return ResolveResult.Failed("Could not find model category in revolving net package");

                    var instances = new List<ResolvedInstance>();

                    var bodyFile = category.Files.FirstOrDefault(x => x.Name == "body");
                    var netFile = category.Files.FirstOrDefault(x => x.Name == "net");

                    if (netFile != null && bodyFile != null)
                    {
                        var xno = context.LoadXno($"/win32/{bodyFile.Location}");
                        var netOffset = Vector3.Zero;

                        if (xno != null)
                        {
                            var objectChunk = xno.GetChunk<ObjectChunk>();
                            var nodeNameChunk = xno.GetChunk<NodeNameChunk>();
                            var node = ResolverContext.FindNodeByName(objectChunk, nodeNameChunk, "netpoint");
                            netOffset = node.Translation;
                        }
                        else
                        {
                            Logger.Warning?.PrintMsg(LogClass.Application, $"Failed to find XNO at {bodyFile.Location}");
                        }

                        instances.Add(new ResolvedInstance
                        {
                            ModelPath = $"/win32/{bodyFile.Location}",
                            Position = setObject.Position,
                            Rotation = setObject.Rotation,
                            Visible = true
                        });

                        instances.Add(new ResolvedInstance
                        {
                            ModelPath = $"/win32/{netFile.Location}",
                            Position = setObject.Position + netOffset,
                            Rotation = setObject.Rotation,
                            Visible = true
                        });
                    }


                    return ResolveResult.WithInstances(instances);
                }
            }

            return ResolveResult.Failed($"Package not found for {setObject.Type}");
        }
    }
}
