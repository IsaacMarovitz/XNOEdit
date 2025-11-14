using System.Numerics;

namespace XNOEdit.Renderer
{
    public class Transform
    {
        public Vector3 Position { get; set; } = new Vector3(0, 0, 0);

        public float Scale { get; set; } = 1f;

        public Quaternion Rotation { get; set; } = Quaternion.Identity;
    }
}
