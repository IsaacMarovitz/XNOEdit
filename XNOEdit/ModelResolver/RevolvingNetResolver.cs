using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public class RevolvingNetResolver : IModelResolver
    {
        public string[] ResolveModel(Package package, StageSetObject setObject)
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
