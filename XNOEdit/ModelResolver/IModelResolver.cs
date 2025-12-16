using Marathon.Formats.Parameter;
using Marathon.Formats.Placement;

namespace XNOEdit.ModelResolver
{
    public interface IModelResolver
    {
        string[] Resolve(Package package, StageSetObject setObject);
        int Priority => 0;
        bool CanResolve(string objectType);
    }
}
