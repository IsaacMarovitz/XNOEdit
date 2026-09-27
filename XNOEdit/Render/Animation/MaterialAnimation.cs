using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Flags;
using XNOEdit.Logging;

namespace XNOEdit.Render.Animation
{
    public struct MaterialValues
    {
        public Vector4 Diffuse;
        public Vector4 Ambient;
        public Vector4 Specular;
        public Vector2 Offset;
        public bool Hidden;
    }

    public sealed class MaterialAnimation
    {
        private readonly record struct Channel(SubMotionType Type, MotionCurve Curve);

        private readonly Dictionary<int, List<Channel>> _channels = [];

        public MaterialAnimation(MaterialMotionChunk chunk)
        {
            foreach (var subMotion in chunk.SubMotions)
            {
                var type = subMotion.Type & SubMotionType.NND_SMOTTYPE_VALUETYPE_MASK;
                var probe = default(MaterialValues);

                if (!Apply(ref probe, type, Vector3.Zero))
                {
                    Logger.Warning?.PrintMsg(LogClass.Application,
                        $"Material {subMotion.NodeIndex}: {PropertyUtility.SubmotionTypeToString(type)} is not applied");
                    continue;
                }

                MotionCurve curve;

                try
                {
                    curve = new MotionCurve(subMotion, MotionValueType.Float);
                }
                catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
                {
                    Logger.Error?.PrintMsg(LogClass.Application, $"Material {subMotion.NodeIndex}: {ex.Message}");
                    continue;
                }

                if (!_channels.TryGetValue(subMotion.NodeIndex, out var channels))
                {
                    channels = [];
                    _channels[subMotion.NodeIndex] = channels;
                }

                channels.Add(new Channel(type, curve));
            }
        }

        public MaterialValues Sample(int material, float frame, in MaterialValues bind)
        {
            var values = bind;

            if (_channels.TryGetValue(material, out var channels))
            {
                foreach (var channel in channels)
                    Apply(ref values, channel.Type, channel.Curve.Sample(frame));
            }

            return values;
        }

        private static bool Apply(ref MaterialValues values, SubMotionType type, Vector3 value)
        {
            switch (type)
            {
                case SubMotionType.NND_SMOTTYPE_HIDE:
                    values.Hidden = value.X != 0.0f;
                    return true;
                case SubMotionType.NND_SMOTTYPE_ALPHA:
                    values.Diffuse.W = value.X;
                    return true;
                case SubMotionType.NND_SMOTTYPE_OFFSET_U:
                    values.Offset.X = value.X;
                    return true;
                case SubMotionType.NND_SMOTTYPE_OFFSET_V:
                    values.Offset.Y = value.X;
                    return true;
            }

            return ApplyColour(ref values.Diffuse, type, value, SubMotionType.NND_SMOTTYPE_DIFFUSE_R)
                || ApplyColour(ref values.Ambient, type, value, SubMotionType.NND_SMOTTYPE_AMBIENT_R)
                || ApplyColour(ref values.Specular, type, value, SubMotionType.NND_SMOTTYPE_SPECULAR_R);
        }

        private static bool ApplyColour(ref Vector4 colour, SubMotionType type, Vector3 value, SubMotionType red)
        {
            var green = (SubMotionType)((uint)red << 1);
            var blue = (SubMotionType)((uint)red << 2);

            if (type == (red | green | blue))
                colour = new Vector4(value, colour.W);
            else if (type == red)
                colour.X = value.X;
            else if (type == green)
                colour.Y = value.X;
            else if (type == blue)
                colour.Z = value.X;
            else
                return false;

            return true;
        }
    }
}
