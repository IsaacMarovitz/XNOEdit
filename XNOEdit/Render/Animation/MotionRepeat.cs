using Marathon.Formats.Ninja.Flags;

namespace XNOEdit.Render.Animation
{
    public enum MotionRepeat
    {
        Clamp,
        Repeat,
        Mirror,
        Offset,
    }

    public static class MotionRepeatExtensions
    {
        public static MotionRepeat ToRepeat(this MotionType type) => (type & MotionType.NND_MOTIONTYPE_REPEAT_MASK) switch
        {
            MotionType.NND_MOTIONTYPE_REPEAT => MotionRepeat.Repeat,
            MotionType.NND_MOTIONTYPE_MIRROR => MotionRepeat.Mirror,
            MotionType.NND_MOTIONTYPE_OFFSET => MotionRepeat.Offset,
            _ => MotionRepeat.Clamp,
        };

        public static MotionRepeat ToRepeat(this SubMotionInterpolationType type) => (type & SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT_MASK) switch
        {
            SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT => MotionRepeat.Repeat,
            SubMotionInterpolationType.NND_SMOTIPTYPE_MIRROR => MotionRepeat.Mirror,
            SubMotionInterpolationType.NND_SMOTIPTYPE_OFFSET => MotionRepeat.Offset,
            _ => MotionRepeat.Clamp,
        };

        public static float Map(this MotionRepeat repeat, float frame, float start, float end, out int cycle)
        {
            cycle = 0;
            var length = end - start;

            if (repeat == MotionRepeat.Clamp || length <= 0.0f)
                return Math.Clamp(frame, start, Math.Max(start, end));

            var periods = (frame - start) / length;
            cycle = (int)MathF.Floor(periods);
            var phase = periods - cycle;

            // Mirror plays every odd period backwards.
            if (repeat == MotionRepeat.Mirror && (cycle & 1) != 0)
                phase = 1.0f - phase;

            return start + phase * length;
        }
    }
}
