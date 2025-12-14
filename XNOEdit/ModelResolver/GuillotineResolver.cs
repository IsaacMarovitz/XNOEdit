using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public class GuillotineResolver : IModelResolver
    {
        public string[] ResolveModel(Package package, StageSetObject setObject)
        {
            string[] models = [];

            var category = package.Categories.FirstOrDefault(x => x.Name == "model");

            var modelAFile = category?.Files.FirstOrDefault(x => x.Name == "modelA");
            var modelBFile = category?.Files.FirstOrDefault(x => x.Name == "modelB");
            var modelCFile = category?.Files.FirstOrDefault(x => x.Name == "modelC");

            models = setObject.Parameters[0].Value switch
            {
                1 => [modelAFile?.Location ?? ""],
                2 => [modelBFile?.Location ?? ""],
                3 => [modelCFile?.Location ?? ""],
                _ => models
            };

            return models.ToArray();
        }
    }
}
