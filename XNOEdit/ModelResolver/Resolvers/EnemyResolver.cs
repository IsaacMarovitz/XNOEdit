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

        private readonly IReadOnlySet<string> _bossVariants = new HashSet<string>
        {
            "firstiblis",
            "secondiblis",
            "thirdiblis",
            "eCerberus",
            "eGenesis",
            "eWyvern",
            "solaris01",
            "solaris02"
        };

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var variant = (string?)setObject.Parameters[0].Value;

            if (variant != null)
            {
                var name = variant.Replace("(Fly)", string.Empty);
                var enemyPath = $"enemy/{FolderForVariant(name)}/en_{FileForVariant(name)}.xno";
                var archive = _bossVariants.Contains(name) ? "win32/archives/enemy_data" : "xenon/archives/enemy";

                return ResolveResult.WithInstance(
                    ResolvedInstance.Create(archive,
                        $"/win32/{enemyPath}",
                        setObject.Position,
                        setObject.Rotation));
            }

            return ResolveResult.Failed($"Failed to find enemy {setObject.Type}");
        }

        private string FolderForVariant(string variant)
        {
            return variant switch
            {
                "firstiblis" => "iblis01",
                "secondiblis" => "iblis02",
                "thirdiblis" => "iblis03",
                "solaris01" => "Solaris01",
                "solaris02" => "Solaris01",
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
                "firstiblis" => "iblis01",
                "secondiblis" => "Iblis02",
                "thirdiblis" => "iblis03",
                "solaris01" => "Solaris01",
                "solaris02" => "Solaris02",
                "cGolem" => "cglm",
                _ => variant
            };
        }
    }
}
