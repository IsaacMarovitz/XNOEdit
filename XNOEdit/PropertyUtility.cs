using System.Numerics;
using Marathon.Formats.Ninja.Flags;
using Marathon.IO.Types;
using BlendOperation = Marathon.Formats.Ninja.Flags.BlendOperation;
using CompareFunction = Marathon.Formats.Ninja.Flags.CompareFunction;

namespace XNOEdit
{
    public static class PropertyUtility
    {
        public static Vector4 MaterialColorToVec4(Colour<float, BGRA> colour)
        {
            return new Vector4(colour.B,  colour.G, colour.R, colour.A);
        }

        public static string BlendModeToString(BlendMode blendMode)
        {
            return blendMode switch
            {
                BlendMode.NNE_BLENDMODE_NONE => "None",
                BlendMode.NNE_BLENDMODE_ADDITIVE => "Additive",
                BlendMode.NNE_BLENDMODE_SRCALPHA => "Source Alpha",
                BlendMode.NNE_BLENDMODE_INVSRCALPHA => "Inv. Source Alpha",
                _ => "Unknown"
            };
        }

        public static (string, string?) MinFilterToString(MinFilter minFilter)
        {
            return minFilter switch
            {
                MinFilter.NND_MIN_NEAREST => ("Nearest", null),
                MinFilter.NND_MIN_LINEAR => ("Linear", null),
                MinFilter.NND_MIN_NEAREST_MIPMAP_NEAREST => ("Nearest", "Nearest"),
                MinFilter.NND_MIN_NEAREST_MIPMAP_LINEAR => ("Nearest", "Linear"),
                MinFilter.NND_MIN_LINEAR_MIPMAP_NEAREST => ("Linear", "Nearest"),
                MinFilter.NND_MIN_LINEAR_MIPMAP_LINEAR => ("Linear", "Linear"),
                MinFilter.NND_MIN_ANISOTROPIC => ("Anisotropic", null),
                MinFilter.NND_MIN_ANISOTROPIC_MIPMAP_NEAREST => ("Anisotropic", "Nearest"),
                MinFilter.NND_MIN_ANISOTROPIC_MIPMAP_LINEAR => ("Anisotropic", "Linear"),
                MinFilter.NND_MIN_ANISOTROPIC4 => ("Anisotropic 4x", null),
                MinFilter.NND_MIN_ANISOTROPIC4_MIPMAP_NEAREST => ("Anisotropic 4x", "Nearest"),
                MinFilter.NND_MIN_ANISOTROPIC4_MIPMAP_LINEAR => ("Anisotropic 4x", "Linear"),
                MinFilter.NND_MIN_ANISOTROPIC8 => ("Anisotropic 8x", null),
                MinFilter.NND_MIN_ANISOTROPIC8_MIPMAP_NEAREST => ("Anisotropic 8x", "Nearest"),
                MinFilter.NND_MIN_ANISOTROPIC8_MIPMAP_LINEAR => ("Anisotropic 8x", "Linear"),
                _ => ("Unknown", null)
            };
        }

        public static string MagFilterToString(MagFilter minFilter)
        {
            return minFilter switch
            {
                MagFilter.NND_MAG_NEAREST => "Nearest",
                MagFilter.NND_MAG_LINEAR => "Linear",
                MagFilter.NND_MAG_ANISOTROPIC => "Anisotropic",
                _ => "Unknown"
            };
        }

        public static string BlendOperationToString(BlendOperation blendOperation)
        {
            return blendOperation switch
            {
                BlendOperation.NNE_BLENDOP_ADD => "Add",
                _ => "Unknown"
            };
        }

        public static string LogicOperationToString(LogicOperation logicOperation)
        {
            return logicOperation switch
            {
                LogicOperation.NNE_LOGICOP_NONE => "None",
                _ => "Unknown"
            };
        }

        public static string CompareFunctionToString(CompareFunction compareFunction)
        {
            return compareFunction switch
            {
                CompareFunction.NNE_CMPFUNC_NEVER => "Never",
                CompareFunction.NNE_CMPFUNC_LESS => "<",
                CompareFunction.NNE_CMPFUNC_EQUAL => "=",
                CompareFunction.NNE_CMPFUNC_LESSEQUAL => "<=",
                CompareFunction.NNE_CMPFUNC_GREATER => ">",
                CompareFunction.NNE_CMPFUNC_NOTEQUAL => "!=",
                CompareFunction.NNE_CMPFUNC_GREATEREQUAL => ">=",
                CompareFunction.NNE_CMPFUNC_ALWAYS => "Always",
                _ => "Unknown"
            };
        }
    }
}
