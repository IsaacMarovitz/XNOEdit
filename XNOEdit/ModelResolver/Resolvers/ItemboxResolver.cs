using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Placement;
using XNOEdit.Logging;

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
            var package = context.FindPackageForType(setObject.Type);
            if (package == null)
                return ResolveResult.Failed($"Package not found for {setObject.Type}");

            var category = package.Categories.FirstOrDefault(x => x.Name == "model");
            if (category == null)
                return ResolveResult.Failed("Could not find model category in itembox package");

            var groundBaseFile = category.Files.FirstOrDefault(x => x.Name == "model_ground_base");
            var instances = new List<ResolvedInstance>();
            var contentsOffset = Vector3.Zero;

            if (setObject.Type == "itemboxg" && groundBaseFile != null)
            {
                var xno = context.LoadXno($"/win32/{groundBaseFile.Location}");

                if (xno != null)
                {
                    var objectChunk = xno.GetChunk<ObjectChunk>();
                    var nodeNameChunk = xno.GetChunk<NodeNameChunk>();
                    var node = ResolverContext.FindNodeByName(objectChunk, nodeNameChunk, "Itempoint_ground");
                    contentsOffset = node.Translation;

                    instances.Add(new ResolvedInstance
                    {
                        ModelPath = $"/win32/{groundBaseFile.Location}",
                        Position = setObject.Position,
                        Rotation = setObject.Rotation,
                        Visible = true
                    });
                }
                else
                {
                    Logger.Warning?.PrintMsg(LogClass.Application, $"Failed to find XNO at {groundBaseFile.Location}");
                }
            }

            var variant = setObject.Parameters.Count > 0 ? setObject.Parameters[0].Value : 0;
            var modelPath = ResolverContext.GetVariantModel(category, (int)variant,
                "model_ring5",
                "model_ring10",
                "model_ring20",
                "model_extend",
                "model_speedup",
                "model_gaugeup",
                "model_invincible",
                "model_barrier");

            if (string.IsNullOrEmpty(modelPath))
                return ResolveResult.Failed($"Could not find requested itembox model {variant}");

            instances.Add(new ResolvedInstance
            {
                ModelPath = $"/win32/{modelPath}",
                Position = setObject.Position + contentsOffset,
                Rotation = setObject.Rotation,
                Visible = true
            });

            return ResolveResult.WithInstances(instances);
        }
    }
}
