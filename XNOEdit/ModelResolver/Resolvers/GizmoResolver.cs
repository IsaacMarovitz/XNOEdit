using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class GizmoResolver : ModelResolver
    {
        // These do not have a visual representation
        // They should be shown as gizmos in a later revision
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "particle",
            "aqa_pond",
            "ambience",
            "ambience_collision",
            "wvo_waterslider",
            "chainjump",
            "cameraeventbox",
            "cameraeventcylinder",
            "eventbox",
            "player_start2",
            "player_goal",
            "amigo_collision",
            "pointsample",
            "positionSample",
            "common_stopplayercollision",
            "common_water_collision",
            "common_hint_collision",
            "common_windcollision_box",
            "impulsesphere",
            "snowboardjump"
        };

        public override int Priority => 20;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            return ResolveResult.Skipped;
        }
    }
}
