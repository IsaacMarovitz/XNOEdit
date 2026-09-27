using System.Numerics;
using Marathon.Formats.Ninja.Chunks;
using Marathon.Formats.Ninja.Flags;
using Marathon.Formats.Ninja.Types;
using XNOEdit.Logging;

namespace XNOEdit.Render.Animation
{
    public struct MaterialValues
    {
        public Vector4 Diffuse;
        public Vector4 Ambient;
        public Vector4 Specular;
        public bool Hidden;
    }

    public sealed class MaterialAnimation
    {
        private readonly record struct Channel(SubMotionType Type, int Map, MotionCurve Curve);

        private readonly Dictionary<int, List<Channel>> _channels = [];

        public MaterialAnimation(MaterialMotionChunk chunk, List<Material> materials)
        {
            foreach (var subMotion in chunk.SubMotions)
            {
                var type = subMotion.Type & SubMotionType.NND_SMOTTYPE_VALUETYPE_MASK;
                var probe = default(MaterialValues);

                var material = subMotion.NodeIndex & 0xFFFF;
                var map = subMotion.NodeIndex >>> 16;

                if (material >= materials.Count)
                {
                    Logger.Error?.PrintMsg(LogClass.Application,
                        $"Submotion targets material {material} of {materials.Count}");
                    continue;
                }

                if (IsOffset(type))
                {
                    if (map >= materials[material].TextureMap.Descriptions.Count)
                    {
                        Logger.Error?.PrintMsg(LogClass.Application,
                            $"Material {material}: offset targets texture map {map} of {materials[material].TextureMap.Descriptions.Count}");
                        continue;
                    }
                }
                else if (!Apply(ref probe, type, Vector3.Zero))
                {
                    Logger.Warning?.PrintMsg(LogClass.Application,
                        $"Material {material}: {PropertyUtility.SubmotionTypeToString(type)} is not applied");
                    continue;
                }

                MotionCurve curve;

                try
                {
                    curve = new MotionCurve(subMotion, MotionValueType.Float);
                }
                catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
                {
                    Logger.Error?.PrintMsg(LogClass.Application, $"Material {material}: {ex.Message}");
                    continue;
                }

                if (!_channels.TryGetValue(material, out var channels))
                {
                    channels = [];
                    _channels[material] = channels;
                }

                channels.Add(new Channel(type, map, curve));
            }
        }

        public MaterialValues Sample(int material, float frame, in MaterialValues bind, Span<Vector2> offsets)
        {
            var values = bind;

            if (_channels.TryGetValue(material, out var channels))
            {
                foreach (var channel in channels)
                {
                    var value = channel.Curve.Sample(frame);

                    if (channel.Type == SubMotionType.NND_SMOTTYPE_OFFSET_U)
                        offsets[channel.Map].X = value.X;
                    else if (channel.Type == SubMotionType.NND_SMOTTYPE_OFFSET_V)
                        offsets[channel.Map].Y = value.X;
                    else
                        Apply(ref values, channel.Type, value);
                }
            }

            return values;
        }

        private static bool IsOffset(SubMotionType type) =>
            type is SubMotionType.NND_SMOTTYPE_OFFSET_U or SubMotionType.NND_SMOTTYPE_OFFSET_V;

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
