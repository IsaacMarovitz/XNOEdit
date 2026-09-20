using Marathon.Formats.Ninja.Flags;
using Marathon.Formats.Ninja.Types;
using Plume;
using Solaris;

namespace XNOEdit.Guest
{
    public readonly record struct GuestMeshState(
        SlBlendState Blend,
        bool BlendEnabled,
        RenderComparisonFunction DepthCompare,
        bool DepthWrite,
        bool DepthTest,
        bool AlphaTest,
        float AlphaThreshold);

    public class GuestRenderState
    {
        public static GuestMeshState FromLogic(MaterialLogic logic) => new(
            ToBlendState(logic),
            logic.Blend,
            ToDepthCompare(logic.ZCompareFunction),
            logic.ZUpdate,
            logic.ZCompare,
            logic.Alpha,
            logic.AlphaRef / 255.0f);

        private static SlBlendState ToBlendState(MaterialLogic logic)
        {
            var source = ToBlend(logic.SourceBlend);
            var destination = ToBlend(logic.DestinationBlend);

            return new SlBlendState(
                source, destination, ToOperation(logic.BlendOperation),
                source, destination, ToOperation(logic.BlendOperation));
        }

        private static RenderBlend ToBlend(BlendMode mode) => mode switch
        {
            BlendMode.NNE_BLENDMODE_NONE => RenderBlend.Zero,
            BlendMode.NNE_BLENDMODE_ADDITIVE => RenderBlend.One,
            BlendMode.NNE_BLENDMODE_SRCALPHA => RenderBlend.SrcAlpha,
            BlendMode.NNE_BLENDMODE_INVSRCALPHA => RenderBlend.InvSrcAlpha,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        private static RenderBlendOperation ToOperation(BlendOperation operation) => operation switch
        {
            BlendOperation.NNE_BLENDOP_ADD => RenderBlendOperation.Add,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        // Flipped for reverse-z
        private static RenderComparisonFunction ToDepthCompare(CompareFunction function) => function switch
        {
            CompareFunction.NNE_CMPFUNC_NEVER => RenderComparisonFunction.Never,
            CompareFunction.NNE_CMPFUNC_LESS => RenderComparisonFunction.Greater,
            CompareFunction.NNE_CMPFUNC_EQUAL => RenderComparisonFunction.Equal,
            CompareFunction.NNE_CMPFUNC_LESSEQUAL => RenderComparisonFunction.GreaterEqual,
            CompareFunction.NNE_CMPFUNC_GREATER => RenderComparisonFunction.Less,
            CompareFunction.NNE_CMPFUNC_NOTEQUAL => RenderComparisonFunction.NotEqual,
            CompareFunction.NNE_CMPFUNC_GREATEREQUAL => RenderComparisonFunction.LessEqual,
            CompareFunction.NNE_CMPFUNC_ALWAYS => RenderComparisonFunction.Always,
            _ => throw new ArgumentOutOfRangeException(nameof(function), function, null)
        };
    }
}
