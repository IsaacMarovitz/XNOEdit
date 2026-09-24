using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Flags;
using Marathon.Formats.Ninja.Types;
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

        public static string MotionTypeToString(MotionType motionType)
        {
            var flagNames = new[] {
                // Motion types
                (MotionType.NND_MOTIONTYPE_NODE,        "Node"),
                (MotionType.NND_MOTIONTYPE_CAMERA,      "Camera"),
                (MotionType.NND_MOTIONTYPE_LIGHT,       "Light"),
                (MotionType.NND_MOTIONTYPE_MORPH,       "Morph"),
                (MotionType.NND_MOTIONTYPE_MATERIAL,    "Material"),

                // Repeat types
                (MotionType.NND_MOTIONTYPE_TRIGGER,     "Trigger"),
                (MotionType.NND_MOTIONTYPE_NOREPEAT,    "No Repeat"),
                (MotionType.NND_MOTIONTYPE_CONSTREPEAT, "Const Repeat"),
                (MotionType.NND_MOTIONTYPE_REPEAT,      "Repeat"),
                (MotionType.NND_MOTIONTYPE_MIRROR,      "Mirror"),
                (MotionType.NND_MOTIONTYPE_OFFSET,      "Offset")
            };

            return string.Join(", ", flagNames.Where(x => motionType.HasFlag(x.Item1)).Select(x => x.Item2));
        }

        public static string SubmotionTypeToString(SubMotionType submotionType)
        {
           var flagNames = new[]
            {
                // Frame types
                (SubMotionType.NND_SMOTTYPE_FRAME_FLOAT, "Frame Float"),
                (SubMotionType.NND_SMOTTYPE_FRAME_SINT16, "Frame SInt16"),

                // Angle types
                (SubMotionType.NND_SMOTTYPE_ANGLE_RADIAN, "Radians"),
                (SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE32, "Angle32"),
                (SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE16, "Angle16"),

                // Node types
                (SubMotionType.NND_SMOTTYPE_QUATERNION, "Quaternion Rotation"),
                (SubMotionType.NND_SMOTTYPE_USER_UINT32, "User UInt32"),
                (SubMotionType.NND_SMOTTYPE_USER_FLOAT, "User Float"),
                (SubMotionType.NND_SMOTTYPE_NODEHIDE, "Hide Node"),

                // Camera types
                (SubMotionType.NND_SMOTTYPE_ROLL, "Roll"),
                (SubMotionType.NND_SMOTTYPE_FOVY, "FOV"),
                (SubMotionType.NND_SMOTTYPE_ZNEAR, "Z Near"),
                (SubMotionType.NND_SMOTTYPE_ZFAR, "Z Far"),
                (SubMotionType.NND_SMOTTYPE_ASPECT, "Aspect Ratio"),

                // Light types
                (SubMotionType.NND_SMOTTYPE_LIGHT_ALPHA, "Light Alpha"),
                (SubMotionType.NND_SMOTTYPE_LIGHT_INTENSITY, "Light Intensity"),
                (SubMotionType.NND_SMOTTYPE_FALLOFF_START, "Falloff Start"),
                (SubMotionType.NND_SMOTTYPE_FALLOFF_END, "Falloff End"),
                (SubMotionType.NND_SMOTTYPE_INNER_ANGLE, "Inner Angle"),
                (SubMotionType.NND_SMOTTYPE_OUTER_ANGLE, "Outer Angle"),
                (SubMotionType.NND_SMOTTYPE_INNER_RANGE, "Inner Range"),
                (SubMotionType.NND_SMOTTYPE_OUTER_RANGE, "Outer Range"),

                // Morph types
                (SubMotionType.NND_SMOTTYPE_MORPH_WEIGHT, "Morph Weight"),

                // Material types
                (SubMotionType.NND_SMOTTYPE_HIDE, "Hide Material"),
                (SubMotionType.NND_SMOTTYPE_ALPHA, "Material Alpha"),
                (SubMotionType.NND_SMOTTYPE_SPECULAR_LEVEL, "Specular Level"),
                (SubMotionType.NND_SMOTTYPE_SPECULAR_GLOSS, "Specular Gloss"),
                (SubMotionType.NND_SMOTTYPE_TEXTURE_INDEX, "Texture Index"),
                (SubMotionType.NND_SMOTTYPE_TEXTURE_BLEND, "Texture Blend"),
                (SubMotionType.NND_SMOTTYPE_MATCLBK_USER, "Material Clbk User")
            };

           var axisGroups = new[]
            {
                // Node types
                ("Translation", [(SubMotionType.NND_SMOTTYPE_TRANSLATION_X, "X"), (SubMotionType.NND_SMOTTYPE_TRANSLATION_Y, "Y"), (SubMotionType.NND_SMOTTYPE_TRANSLATION_Z, "Z")]),
                ("Rotation", [(SubMotionType.NND_SMOTTYPE_ROTATION_X, "X"), (SubMotionType.NND_SMOTTYPE_ROTATION_Y, "Y"), (SubMotionType.NND_SMOTTYPE_ROTATION_Z, "Z")]),
                ("Scaling", [(SubMotionType.NND_SMOTTYPE_SCALING_X, "X"), (SubMotionType.NND_SMOTTYPE_SCALING_Y, "Y"), (SubMotionType.NND_SMOTTYPE_SCALING_Z, "Z")]),

                // Camera types
                ("Target", [(SubMotionType.NND_SMOTTYPE_TARGET_X, "X"), (SubMotionType.NND_SMOTTYPE_TARGET_Y, "Y"), (SubMotionType.NND_SMOTTYPE_TARGET_Z, "Z")]),
                ("Up Target", [(SubMotionType.NND_SMOTTYPE_UPTARGET_X, "X"), (SubMotionType.NND_SMOTTYPE_UPTARGET_Y, "Y"), (SubMotionType.NND_SMOTTYPE_UPTARGET_Z, "Z")]),
                ("Up Vector", [(SubMotionType.NND_SMOTTYPE_UPVECTOR_X, "X"), (SubMotionType.NND_SMOTTYPE_UPVECTOR_Y, "Y"), (SubMotionType.NND_SMOTTYPE_UPVECTOR_Z, "Z")]),

                // Light types
                ("Light Color", [(SubMotionType.NND_SMOTTYPE_LIGHT_COLOR_R, "R"), (SubMotionType.NND_SMOTTYPE_LIGHT_COLOR_G, "G"), (SubMotionType.NND_SMOTTYPE_LIGHT_COLOR_B, "B")]),

                // Material types
                ("Diffuse", [(SubMotionType.NND_SMOTTYPE_DIFFUSE_R, "R"), (SubMotionType.NND_SMOTTYPE_DIFFUSE_G, "G"), (SubMotionType.NND_SMOTTYPE_DIFFUSE_B, "B")]),
                ("Specular", [(SubMotionType.NND_SMOTTYPE_SPECULAR_R, "R"), (SubMotionType.NND_SMOTTYPE_SPECULAR_G, "G"), (SubMotionType.NND_SMOTTYPE_SPECULAR_B, "B")]),
                ("Ambient", [(SubMotionType.NND_SMOTTYPE_AMBIENT_R, "R"), (SubMotionType.NND_SMOTTYPE_AMBIENT_G, "G"), (SubMotionType.NND_SMOTTYPE_AMBIENT_B, "B")]),
                ("Offset", new[] { (SubMotionType.NND_SMOTTYPE_OFFSET_U, "U"), (SubMotionType.NND_SMOTTYPE_OFFSET_V, "V") })
            };

            var names = flagNames.Where(x => submotionType.HasFlag(x.Item1)).Select(x => x.Item2);

            var axisNames = axisGroups
                .Select(x => (Axes: string.Concat(x.Item2.Where(y => submotionType.HasFlag(y.Item1)).Select(y => y.Item2)), Name: x.Item1))
                .Where(x => x.Axes.Length > 0)
                .Select(x => $"{x.Axes} {x.Name}");

            return string.Join(", ", names.Concat(axisNames));
        }

        public static string SubmotionInterpolationTypeToString(SubMotionInterpolationType interpolationType)
        {
            var flagNames = new[] {
                // Repeat types
                (SubMotionInterpolationType.NND_SMOTIPTYPE_NOREPEAT,    "No Repeat"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTREPEAT, "Const Repeat"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT,      "Repeat"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_MIRROR,      "Mirror"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_OFFSET,      "Offset"),

                // Interpolation types
                (SubMotionInterpolationType.NND_SMOTIPTYPE_SPLINE,     "Spline"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_LINEAR,     "Linear"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT,   "Constant"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_BEZIER,     "Bezier"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_SI_SPLINE,  "SI Spline"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_TRIGGER,    "Trigger"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_QUAT_LERP,  "Quat Lerp"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_QUAT_SLERP, "Quat Slerp"),
                (SubMotionInterpolationType.NND_SMOTIPTYPE_QUAT_SQUAD, "Quat Squad"),
            };

            return string.Join(", ", flagNames.Where(x => interpolationType.HasFlag(x.Item1)).Select(x => x.Item2));
        }

        public static string GetNodeName(NodeNameChunk? nameChunk, int index)
        {
            var name = nameChunk?.Names.ElementAtOrDefault(index);

            return string.IsNullOrEmpty(name) ? $"<Node {index + 1}>" : name;
        }

        public static Node? FindNodeByName(ObjectChunk objectChunk, NodeNameChunk? nameChunk, string name)
        {
            return objectChunk.Nodes
                .Where((_, index) => GetNodeName(nameChunk, index) == name)
                .FirstOrDefault();
        }
    }
}
