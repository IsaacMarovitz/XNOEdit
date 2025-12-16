using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class PhysicsObjectResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "objectphysics",
            "objectphysics_item"
        };

        public override int Priority => 20;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var objectNameIndex = context.Actor.Parameters
                .Select((param, index) => (param, index))
                .Where(pair => pair.param.Name == "objectName")
                .Select(pair => pair.index)
                .FirstOrDefault(-1);

            if (objectNameIndex == -1)
                return ResolveResult.Empty;

            var modelName = (string)setObject.Parameters[objectNameIndex].Value;
            var physicsParam = context.ObjectParameters.FirstOrDefault(x => x.Name == modelName);

            if (physicsParam == null)
                return ResolveResult.Empty;

            return ResolveResult.WithInstance(
                ResolvedInstance.Create(physicsParam.Model, setObject.Position, setObject.Rotation));
        }
    }
}
