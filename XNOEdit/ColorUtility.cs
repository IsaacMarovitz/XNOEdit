using System.Numerics;
using Marathon.IO.Types;

namespace XNOEdit
{
    public static class ColorUtility
    {
        public static Vector4 MaterialColorToVec4(Colour<float, BGRA> colour)
        {
            return new Vector4(colour.B,  colour.G, colour.R, colour.A);
        }
    }
}
