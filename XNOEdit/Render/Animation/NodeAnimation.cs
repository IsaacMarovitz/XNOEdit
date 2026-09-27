using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Flags;
using XNOEdit.Logging;

namespace XNOEdit.Render.Animation
{
    public sealed class NodeAnimation
    {
        private readonly record struct Channel(int Node, SubMotionType Type, MotionCurve Curve);

        private readonly List<Channel> _channels = [];

        public NodeAnimation(MotionChunk chunk, int nodeCount)
        {
            foreach (var subMotion in chunk.SubMotions)
            {
                var type = subMotion.Type & SubMotionType.NND_SMOTTYPE_VALUETYPE_MASK;
                var probe = default(NodeTransform);

                if (subMotion.NodeIndex < 0 || subMotion.NodeIndex >= nodeCount)
                {
                    Logger.Error?.PrintMsg(LogClass.Application,
                        $"Submotion targets node {subMotion.NodeIndex} of {nodeCount}");
                    continue;
                }

                if (!Apply(ref probe, type, Vector3.Zero))
                {
                    Logger.Warning?.PrintMsg(LogClass.Application,
                        $"Node {subMotion.NodeIndex}: {PropertyUtility.SubmotionTypeToString(type)} is not applied");
                    continue;
                }

                // Marathon reads every XYZ rotation as A16 keys; A32 ones would be misparsed.
                if (type == SubMotionType.NND_SMOTTYPE_ROTATION_XYZ &&
                    !subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE16))
                {
                    Logger.Error?.PrintMsg(LogClass.Application,
                        $"Node {subMotion.NodeIndex}: XYZ rotation keys are not A16");
                    continue;
                }

                var valueType = MotionValueType.Float;

                if (IsRotation(type))
                {
                    if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE16))
                    {
                        valueType = MotionValueType.Angle16;
                    }
                    else if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_ANGLE_ANGLE32))
                    {
                        valueType = MotionValueType.Angle32;
                    }
                    else
                    {
                        Logger.Error?.PrintMsg(LogClass.Application,
                            $"Node {subMotion.NodeIndex}: rotation keys are neither A16 nor A32");
                        continue;
                    }
                }

                MotionCurve curve;

                try
                {
                    curve = new MotionCurve(subMotion, valueType);
                }
                catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
                {
                    Logger.Error?.PrintMsg(LogClass.Application, $"Node {subMotion.NodeIndex}: {ex.Message}");
                    continue;
                }

                _channels.Add(new Channel(subMotion.NodeIndex, type, curve));
            }
        }

        /// <summary>Overwrites the animated channels of <paramref name="transforms"/>, which start as the bind pose.</summary>
        public void Sample(float frame, Span<NodeTransform> transforms)
        {
            foreach (var channel in _channels)
                Apply(ref transforms[channel.Node], channel.Type, channel.Curve.Sample(frame));
        }

        private static bool IsRotation(SubMotionType type) =>
            type != 0 && (type & ~SubMotionType.NND_SMOTTYPE_ROTATION_XYZ) == 0;

        private static bool Apply(ref NodeTransform transform, SubMotionType type, Vector3 value)
        {
            return ApplyAxes(ref transform.Translation, type, value, SubMotionType.NND_SMOTTYPE_TRANSLATION_X)
                || ApplyAxes(ref transform.Rotation, type, value * Skeleton.AngleToRadians, SubMotionType.NND_SMOTTYPE_ROTATION_X)
                || ApplyAxes(ref transform.Scale, type, value, SubMotionType.NND_SMOTTYPE_SCALING_X);
        }

        /// <summary>
        /// Node channels are three consecutive bits from X; a submotion drives either
        /// one axis with scalar keys or all three with vector keys.
        /// </summary>
        private static bool ApplyAxes(ref Vector3 target, SubMotionType type, Vector3 value, SubMotionType x)
        {
            var y = (SubMotionType)((uint)x << 1);
            var z = (SubMotionType)((uint)x << 2);

            if (type == (x | y | z))
                target = value;
            else if (type == x)
                target.X = value.X;
            else if (type == y)
                target.Y = value.X;
            else if (type == z)
                target.Z = value.X;
            else
                return false;

            return true;
        }
    }
}
