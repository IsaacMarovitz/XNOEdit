using System.Numerics;

namespace XNOEdit
{
    public class RenderSettings
    {
        public bool ShowGrid = true;
        public bool BackfaceCulling = false;
        public float CameraSensitivity = 1.0f;
        public Vector3 SunDirection = Vector3.Normalize(new Vector3(0.5f, 0.5f, 0.5f));
        public Vector3 SunColor = new(0.98f, 0.94f, 0.91f);
    }
}
