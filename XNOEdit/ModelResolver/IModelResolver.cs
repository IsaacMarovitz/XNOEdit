using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public interface IModelResolver
    {
        string[] ResolveModel(Package package, StageSetObject setObject);
    }
}
