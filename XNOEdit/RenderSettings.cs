using System.Numerics;

namespace XNOEdit
{
    public struct RenderSettings
    {
        public bool WireframeMode = false;
        public bool ShowGrid = true;
        public bool VertexColors = true;
        public bool BackfaceCulling = false;
        public Vector3 SunDirection = Vector3.Normalize(new Vector3(0.5f, 0.5f, 0.5f));
        public Vector3 SunColor = Vector3.Normalize(new Vector3(1.0f, 0.95f, 0.8f));

        public RenderSettings() { }
    }
}
