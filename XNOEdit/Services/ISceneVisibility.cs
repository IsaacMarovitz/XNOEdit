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
        private readonly Dictionary<int, bool> _subobjectVisibility = new();
        private readonly Dictionary<(int subobject, int meshSet), bool> _meshSetVisibility = new();

        public event Action<int, int?, bool>? VisibilityChanged;

        public bool GetXnoVisible(int xnoIndex) => true;
        public void SetXnoVisible(int xnoIndex, bool visible) { }

        public bool GetSubobjectVisible(int xnoIndex, int subobjectIndex)
        {
            return !_subobjectVisibility.TryGetValue(subobjectIndex, out var visible) || visible;
        }

        public void SetSubobjectVisible(int xnoIndex, int subobjectIndex, bool visible)
        {
            _subobjectVisibility[subobjectIndex] = visible;
            VisibilityChanged?.Invoke(subobjectIndex, null, visible);
        }

        public bool GetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex)
        {
            return !_meshSetVisibility.TryGetValue((subobjectIndex, meshSetIndex), out var visible) || visible;
        }

        public void SetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex, bool visible)
        {
            _meshSetVisibility[(subobjectIndex, meshSetIndex)] = visible;
            VisibilityChanged?.Invoke(subobjectIndex, meshSetIndex, visible);
        }
    }

    public class StageSceneVisibility : ISceneVisibility
    {
        private readonly Dictionary<int, bool> _xnoVisibility = new();
        private readonly Dictionary<(int xno, int subobject), bool> _subobjectVisibility = new();
        private readonly Dictionary<(int xno, int subobject, int meshSet), bool> _meshSetVisibility = new();

        public event Action<int, bool>? XnoVisibilityChanged;
        public event Action<int, int, int?, bool>? ObjectVisibilityChanged;

        public bool GetXnoVisible(int xnoIndex)
        {
            return !_xnoVisibility.TryGetValue(xnoIndex, out var visible) || visible;
        }

        public void SetXnoVisible(int xnoIndex, bool visible)
        {
            _xnoVisibility[xnoIndex] = visible;
            XnoVisibilityChanged?.Invoke(xnoIndex, visible);
        }

        public bool GetSubobjectVisible(int xnoIndex, int subobjectIndex)
        {
            return !_subobjectVisibility.TryGetValue((xnoIndex, subobjectIndex), out var visible) || visible;
        }

        public void SetSubobjectVisible(int xnoIndex, int subobjectIndex, bool visible)
        {
            _subobjectVisibility[(xnoIndex, subobjectIndex)] = visible;
            ObjectVisibilityChanged?.Invoke(xnoIndex, subobjectIndex, null, visible);
        }

        public bool GetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex)
        {
            return !_meshSetVisibility.TryGetValue((xnoIndex, subobjectIndex, meshSetIndex), out var visible) || visible;
        }

        public void SetMeshSetVisible(int xnoIndex, int subobjectIndex, int meshSetIndex, bool visible)
        {
            _meshSetVisibility[(xnoIndex, subobjectIndex, meshSetIndex)] = visible;
            ObjectVisibilityChanged?.Invoke(xnoIndex, subobjectIndex, meshSetIndex, visible);
        }
    }
}
