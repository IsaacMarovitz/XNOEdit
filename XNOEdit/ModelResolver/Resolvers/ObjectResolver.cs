using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class ObjectResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "objectphysics",
            "objectphysics_item",
            "physicspath",
            "common_path_obj",
        };

        public override int Priority => 20;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var actor = context.Actors.Find(x => x.Name == setObject.Type);

            var objectNameIndex = actor.Parameters
                .Select((param, index) => (param, index))
                .Where(pair => pair.param.Name == "objectName")
                .Select(pair => pair.index)
                .FirstOrDefault(-1);

            if (objectNameIndex == -1)
                return ResolveResult.Empty;

            var modelName = (string)setObject.Parameters[objectNameIndex].Value;
            string modelPath;

            if (setObject.Type == "common_path_obj")
            {
                var pathParam = context.PathParameters.FirstOrDefault(x => x.Name == modelName);

                if (pathParam == null)
                    return ResolveResult.Failed($"Unable to find path parameter '{modelName}'");

                modelPath = pathParam.Model;
            }
            else
            {
                var physicsParam = context.PhysicsParameters.FirstOrDefault(x => x.Name == modelName);

                if (physicsParam == null)
                    return ResolveResult.Failed($"Unable to find physics parameter '{modelName}'");

                modelPath = physicsParam.Model;
            }

            return ResolveResult.WithInstance(
                ResolvedInstance.Create($"/win32/{modelPath}", setObject.Position, setObject.Rotation));
        }
    }
}
