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
