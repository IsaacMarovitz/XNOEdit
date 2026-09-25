using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class TownsmanResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "townsman"
        };

        public override int Priority => 10;

        public override ResolveResult Resolve(ResolverContext context, StageSetObject setObject)
        {
            var manType = (int)setObject.Parameters[1].Value;

            var modelName = manType switch
            {
                0 => null,
                1 => "youngman01",
                2 => "boy01",
                3 => "man01",
                4 => "oldman01",
                5 => "woman01",
                6 => "stationman01",
                7 => "soleanasoldier01",
                8 => "gondolaride01",
                9 => "girl01",
                10 => "youngwoman01",
                11 => "oldwoman01",
                12 => "thief01",
                13 => "accordionist01",
                14 => "kids01",
                15 => "waiter01",
                16 => "black01",
                17 => "shopman01",
                18 => "suitman01",
                19 => "suitwoman01",
                20 => "rivalman01",
                21 => "cameracrew01",
                22 => "painter01",
                23 => "worker01",
                24 => "maidmaster01",
                25 => "youngman02",
                26 => "boy02",
                27 => "man02",
                28 => "oldman02",
                29 => "woman02",
                30 => "girl02",
                31 => "youngwoman02",
                32 => "oldwoman02",
                33 => "kids02",
                34 => "black02",
                35 => "youngman03",
                36 => "youngwoman03",
                37 => "gunsoldier",
                38 => "maid01",
                39 => "priest01",
                40 => "priestmaster01",
                41 => "archaelogist01",
                42 => "agent01",
                43 => "dog01",
                44 => "pigeon01",
                45 => "director01",
                46 => "black03",
                47 => "black04",
                _ => null
            };

            // TODO: Hair and Speak models
            var modelPath = $"/win32/human/town/model/{modelName}/body/cht_{modelName}.xno";

            return ResolveResult.WithInstance(
                ResolvedInstance.Create("xenon/archives/human", modelPath, setObject.Position, setObject.Rotation));
        }
    }
}
