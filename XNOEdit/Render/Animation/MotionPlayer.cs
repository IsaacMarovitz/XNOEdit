using Marathon.Formats.Ninja.Chunks;

namespace XNOEdit.Render.Animation
{
    public sealed class MotionPlayer
    {
        private readonly MotionRepeat _repeat;
        private float _time;

        public MotionPlayer(MotionChunk motion)
            : this(motion.StartFrame, motion.EndFrame, motion.FPS, motion.Type.ToRepeat())
        {
        }

        public MotionPlayer(float startFrame, float endFrame, float fps, MotionRepeat repeat)
        {
            StartFrame = startFrame;
            EndFrame = endFrame;
            Fps = fps;
            _repeat = repeat;
            _time = startFrame;

            Playing = _repeat is MotionRepeat.Repeat or MotionRepeat.Mirror;
        }

        public float StartFrame { get; }
        public float EndFrame { get; }
        public float Fps { get; }
        public bool Playing { get; private set; }

        public float Frame => _repeat.Map(_time, StartFrame, EndFrame, out _);

        public void TogglePlaying()
        {
            // Play on a finished clamped motion restarts it.
            if (!Playing && _repeat == MotionRepeat.Clamp && _time >= EndFrame)
                _time = StartFrame;

            Playing = !Playing;
        }

        public void Advance(float deltaTime)
        {
            if (!Playing)
                return;

            _time += deltaTime * Fps;

            if (_repeat == MotionRepeat.Clamp && _time >= EndFrame)
            {
                _time = EndFrame;
                Playing = false;
            }
        }

        public void Seek(float frame)
        {
            _time = frame;
        }
    }
}
