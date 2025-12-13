using XNOEdit.Renderer.Renderers;

namespace XNOEdit.Services
{
    public interface ISceneVisibility
    {
        bool GetXnoVisible(int xnoIndex);
        void SetXnoVisible(int xnoIndex, bool visible);
        bool GetSubobjectVisible(int xnoIndex, int subobjectIndex);
        void SetSubobjectVisible(int xnoIndex, int subobjectIndex, bool visible);
        bool GetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex);
        void SetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex, bool visible);
    }

    public class ObjectSceneVisibility : ISceneVisibility
    {
        private readonly ModelRenderer? _renderer;

        public event Action<int, int?, bool>? VisibilityChanged;

        public ObjectSceneVisibility(ModelRenderer renderer)
        {
            _renderer = renderer;
        }

        public bool GetXnoVisible(int xnoIndex) => true;
        public void SetXnoVisible(int xnoIndex, bool visible) { }

        public bool GetSubobjectVisible(int xnoIndex, int subobjectIndex)
        {
            return _renderer?.GetSubobjectVisible(subobjectIndex) ?? true;
        }

        public void SetSubobjectVisible(int xnoIndex, int subobjectIndex, bool visible)
        {
            _renderer?.SetVisible(subobjectIndex, null, visible);
            VisibilityChanged?.Invoke(subobjectIndex, null, visible);
        }

        public bool GetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex)
        {
            return _renderer?.GetMeshSetVisible(subobjectIndex, meshSetIndex) ?? true;
        }

        public void SetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex, bool visible)
        {
            _renderer?.SetVisible(subobjectIndex, meshSetIndex, visible);
            VisibilityChanged?.Invoke(subobjectIndex, meshSetIndex, visible);
        }
    }

    public class StageSceneVisibility : ISceneVisibility
    {
        private List<ModelRenderer>? _renderers;

        public event Action<int, bool>? XnoVisibilityChanged;
        public event Action<int, int, int?, bool>? ObjectVisibilityChanged;

        public StageSceneVisibility(List<ModelRenderer> renderers)
        {
            _renderers = renderers;
        }

        public bool GetXnoVisible(int xnoIndex)
        {
            return _renderers?[xnoIndex].GetVisible() ?? true;
        }

        public void SetXnoVisible(int xnoIndex, bool visible)
        {
            _renderers?[xnoIndex].SetVisible(visible);
            XnoVisibilityChanged?.Invoke(xnoIndex, visible);
        }

        public bool GetSubobjectVisible(int xnoIndex, int subobjectIndex)
        {
            return _renderers?[xnoIndex].GetSubobjectVisible(subobjectIndex) ?? true;
        }

        public void SetSubobjectVisible(int xnoIndex, int subobjectIndex, bool visible)
        {
            _renderers?[xnoIndex].SetVisible(subobjectIndex, null, visible);
            ObjectVisibilityChanged?.Invoke(xnoIndex, subobjectIndex, null, visible);
        }

        public bool GetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex)
        {
            return _renderers?[xnoIndex].GetMeshSetVisible(subobjectIndex, meshSetIndex) ?? true;
        }

        public void SetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex, bool visible)
        {
            _renderers?[xnoIndex].SetVisible(subobjectIndex, meshSetIndex, visible);
            ObjectVisibilityChanged?.Invoke(xnoIndex, subobjectIndex, meshSetIndex, visible);
        }
    }
}
