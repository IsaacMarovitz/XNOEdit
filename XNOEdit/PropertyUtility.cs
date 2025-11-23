using System.Numerics;
using Marathon.Formats.Ninja.Flags;
using Marathon.IO.Types;

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
                BlendMode.NNE_BLENDMODE_INVSRCALPHA => "Inverse Source Alpha",
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
