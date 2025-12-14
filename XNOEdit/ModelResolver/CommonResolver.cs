using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public class CommonResolver : IModelResolver
    {
        public string[] ResolveModel(Package package, StageSetObject setObject)
        {
            string[] models = [];

            var category = package.Categories.FirstOrDefault(x => x.Name == "model");

            var modelFile = category?.Files.FirstOrDefault(x => x.Name == "model");

            if (modelFile != null)
                models = [modelFile.Location];

            return models;
        }
    }
}
