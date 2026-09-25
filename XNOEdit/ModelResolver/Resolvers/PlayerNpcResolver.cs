using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class PlayerNpcResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "player_npc"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var variant = (int)setObject.Parameters[0].Value;

            var archiveName = variant switch
            {
                1 => "sonic",
                2 => "shadow",
                3 => "silver",
                // Sonic + Elise
                4 => "sonic",
                5 => "tails",
                6 => "knuckles",
                7 => "amy",
                8 => "blaze",
                9 => "rouge",
                10 => "omega",
                11 => "supersonic",
                12 => "supershadow",
                13 => "supersilver",
                14 => "princess",
                _ => null
            };

            var archive = $"win32/archives/player_{archiveName}";
            var instances = new List<ResolvedInstance>();

            var bodyFile = variant switch
            {
                1 => "sonic_new/sonic_Root.xno",
                2 => "shadow/shadow_Root.xno",
                3 => "silver/silver_Root.xno",
                // Sonic + Elise
                4 => "sonic_new/sonic_Root.xno",
                5 => "tails/tails_Root.xno",
                6 => "knuckles/knuckles_Root.xno",
                7 => "amy/amy_Root.xno",
                8 => "blaze/blaze_Root.xno",
                9 => "rouge/rouge_Root.xno",
                10 => "omega/omega_Root.xno",
                11 => "supersonic/ssonic_Root.xno",
                12 => "supershadow/sshadow_Root.xno",
                13 => "supersilver/ssilver_Root.xno",
                14 => "princess/ch_princess01.xno",
                _ => null
            };

            instances.Add(new ResolvedInstance
            {
                ArchiveHint = archive,
                ModelPath = $"/win32/player/{bodyFile}",
                Position = setObject.Position,
                Rotation = setObject.Rotation,
            });

            var faceFile = variant switch
            {
                1 => "sonic_new/sonic_Head01.xno",
                2 => "shadow/shadow_Head01.xno",
                3 => "silver/silver_Head01.xno",
                // Sonic + Elise
                4 => "sonic_new/sonic_Head01.xno",
                5 => "tails/tails_Head01.xno",
                6 => "knuckles/knuckles_Head01.xno",
                7 => "amy/amy_Head01.xno",
                8 => "blaze/blaze_Head01.xno",
                9 => "rouge/rouge_Head01.xno",
                // Omega doesn't have a separate face.
                10 => null,
                11 => "supersonic/ssonic_Head01.xno",
                12 => "supershadow/sshadow_Head01.xno",
                13 => "supersilver/ssilver_Head01.xno",
                // Also needs ch_princess01_hair.xno
                14 => "princess/ch_princess01_head.xno",
                _ => null
            };

            if (faceFile != null)
            {
                instances.Add(new ResolvedInstance
                {
                    ArchiveHint = archive,
                    ModelPath = $"/win32/player/{faceFile}",
                    Position = setObject.Position,
                    Rotation = setObject.Rotation,
                });
            }

            return ResolveResult.WithInstances(instances);
        }
    }
}
