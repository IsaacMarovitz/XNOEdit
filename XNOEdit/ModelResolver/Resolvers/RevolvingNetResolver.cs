using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver.Resolvers
{
    public class RevolvingNetResolver : ModelResolver
    {
        protected override IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>
        {
            "wvo_revolvingnet"
        };

        public override int Priority => 10;

        public override string[] Resolve(Package package, StageSetObject setObject)
        {
            List<string> models = [];

            var category = package.Categories.FirstOrDefault(x => x.Name == "model");

            var bodyFile = category?.Files.FirstOrDefault(x => x.Name == "body");
            var netFile = category?.Files.FirstOrDefault(x => x.Name == "net");

            if (bodyFile != null)
                models.Add(bodyFile.Location);

            if (netFile != null)
                models.Add(netFile.Location);

            return models.ToArray();
        }
    }
}
