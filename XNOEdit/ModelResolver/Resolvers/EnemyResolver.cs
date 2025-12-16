using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class EnemyResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "enemy",
            "enemyextra"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var variant = setObject.Parameters[0].Value;

            if (variant != null)
            {
                var enemyPath = $"/enemy/{FolderForVariant((string)variant)}/en_{FileForVariant((string)variant)}.xno";

                return ResolveResult.WithInstance(
                    ResolvedInstance.Create("enemy", enemyPath, setObject.Position, setObject.Rotation));
            }

            return ResolveResult.Failed($"Failed to find enemy {setObject.Type}");
        }

        private string FolderForVariant(string variant)
        {
            return variant switch
            {
                "cStalker" => "cBiter",
                "cGazer" => "cCrawler",
                "cTitan" => "cGolem",
                "cTricker" => "cTaker",
                "eArmor" => "eBomber",
                "eSweeper" => "eBomber",
                "eWalker" => "eCannon",
                "eBluster" => "eFlyer",
                "eKeeper" => "eGuardian",
                "eBuster" => "eGunner",
                "eStinger" => "eGunner",
                "eLancer" => "eGunner",
                "echaser" => "eLiner",
                "eCommander" => "eRounder",
                "eHunter" => "eSearcher",
                _ => variant
            };
        }

        private string FileForVariant(string variant)
        {
            return variant switch
            {
                "cGolem" => "cglm",
                _ => variant
            };
        }
    }
}
