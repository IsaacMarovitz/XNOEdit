using System.Numerics;
using Marathon.Formats.Ninja.Flags;
using Marathon.Formats.Ninja.Types;

namespace XNOEdit.Render.Animation
{
    public enum MotionValueType
    {
        Float,
        Angle32,
        Angle16,
    }

    public sealed class MotionCurve
    {
        private const float Turn16 = 65536.0f;

        private readonly float[] _frames;
        private readonly Vector3[] _values;
        private readonly bool _stepped;
        private readonly MotionValueType _valueType;
        private readonly MotionRepeat _repeat;
        private readonly float _start;
        private readonly float _end;

        public MotionCurve(SubMotion subMotion, MotionValueType valueType)
        {
            if (subMotion.Keyframes.Count == 0)
                throw new InvalidDataException("Submotion has no keyframes");

            _stepped = (subMotion.InterpolationType & SubMotionInterpolationType.NND_SMOTIPTYPE_IP_MASK) switch
            {
                SubMotionInterpolationType.NND_SMOTIPTYPE_LINEAR => false,
                SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT => true,
                var other => throw new NotSupportedException($"Unsupported interpolation {other}"),
            };

            _valueType = valueType;
            _repeat = subMotion.InterpolationType.ToRepeat();
            _start = subMotion.StartKeyframe;
            _end = subMotion.EndKeyframe;

            _frames = new float[subMotion.Keyframes.Count];
            _values = new Vector3[subMotion.Keyframes.Count];

            for (var i = 0; i < subMotion.Keyframes.Count; i++)
                (_frames[i], _values[i]) = Decode(subMotion.Keyframes[i], valueType);
        }

        public Vector3 Sample(float frame)
        {
            var local = _repeat.Map(frame, _start, _end, out var cycle);
            var value = Interpolate(local);

            // Offset repeat carries each period's net change into the next.
            if (_repeat == MotionRepeat.Offset)
                value += cycle * (_values[^1] - _values[0]);

            return value;
        }

        private Vector3 Interpolate(float frame)
        {
            var index = Array.BinarySearch(_frames, frame);

            if (index >= 0)
                return _values[index];

            // The complement is the first key after the frame.
            var next = ~index;

            if (next == 0)
                return _values[0];

            if (next == _frames.Length)
                return _values[^1];

            var previous = next - 1;

            if (_stepped)
                return _values[previous];

            var t = (frame - _frames[previous]) / (_frames[next] - _frames[previous]);

            var from = _values[previous];
            var delta = _values[next] - from;

            if (_valueType == MotionValueType.Angle16)
                delta = Wrap16(delta);

            return from + delta * t;
        }

        private static Vector3 Wrap16(Vector3 delta) => new(
            delta.X - Turn16 * MathF.Round(delta.X / Turn16),
            delta.Y - Turn16 * MathF.Round(delta.Y / Turn16),
            delta.Z - Turn16 * MathF.Round(delta.Z / Turn16));

        private static (float Frame, Vector3 Value) Decode(object keyframe, MotionValueType valueType) => keyframe switch
        {
            KeyframeF32 key => (key.Frame, new Vector3(valueType == MotionValueType.Angle32 ? BitConverter.SingleToInt32Bits(key.Value) : key.Value, 0.0f, 0.0f)),
            KeyframeS16 key => (key.Frame, new Vector3(key.Value, 0.0f, 0.0f)),
            KeyframeVector key => (key.Frame, key.Value),
            KeyframeRotateS16 key => (key.Frame, new Vector3(key.X, key.Y, key.Z)),
            _ => throw new NotSupportedException($"Unsupported keyframe {keyframe.GetType().Name}"),
        };
    }
}
