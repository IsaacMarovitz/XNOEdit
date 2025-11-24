using System.Numerics;

namespace XNOEdit
{
    public struct RenderSettings
    {
        public bool WireframeMode = false;
        public bool ShowGrid = true;
        public bool VertexColors = true;
        public bool BackfaceCulling = false;
        public bool Lightmap = true;
        public Vector3 SunDirection = Vector3.Normalize(new Vector3(0.5f, 0.5f, 0.5f));
        public Vector3 SunColor = new(0.98f, 0.94f, 0.91f);

        public RenderSettings() { }
    }
}
