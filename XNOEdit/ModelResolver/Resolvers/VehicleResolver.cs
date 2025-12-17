using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class VehicleResolver : ModelResolver
    {
         protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "vehicle",
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var variant = setObject.Parameters.Count > 0 ? setObject.Parameters[0].Value : 0;

            // Actual packages are in scripts.arc
            // We can skip that and just load the objects directly

            var vehicle = (int)variant switch
            {
                1 => "Jeep",
                2 => "Bike",
                3 => "Hover",
                4 => "Glider"
            };

            var modelPath = $"object/Common/vehicle/Gadget_{vehicle}.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create($"/win32/{modelPath}", setObject.Position, setObject.Rotation));
        }
    }
}
