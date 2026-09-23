namespace Solaris
{
    /// <summary>
    /// Defers destruction until every frame that could reference a resource has completed.
    /// </summary>
    public sealed class SlRetirementQueue
    {
        private readonly struct Entry(ulong retireAfter, Action release)
        {
            public readonly ulong RetireAfter = retireAfter;
            public readonly Action Release = release;
        }

        private readonly List<Entry> _pending = [];
        private readonly int _framesInFlight;
        private readonly Lock _lock = new();

        private ulong _currentFrame;

        public int PendingCount
        {
            get
            {
                lock (_lock)
                {
                    return _pending.Count;
                }
            }
        }

        public SlRetirementQueue(int framesInFlight)
        {
            if (framesInFlight < 1)
                throw new ArgumentOutOfRangeException(nameof(framesInFlight));

            _framesInFlight = framesInFlight;
        }

        public ulong CurrentFrame => _currentFrame;

        /// <summary>Queue an arbitrary release action. Safe to call from any thread.</summary>
        public void Retire(Action release)
        {
            ArgumentNullException.ThrowIfNull(release);

            lock (_lock)
            {
                _pending.Add(new Entry(_currentFrame + (ulong)_framesInFlight, release));
            }
        }

        public void Retire(IDisposable resource)
        {
            ArgumentNullException.ThrowIfNull(resource);

            Retire(resource.Dispose);
        }

        /// <summary>
        /// Advances the timeline and releases everything whose retirement frame has passed.
        /// Called once per frame by the frame graph, before any recording.
        /// </summary>
        public void BeginFrame()
        {
            List<Action>? ready = null;

            lock (_lock)
            {
                _currentFrame++;

                for (var i = _pending.Count - 1; i >= 0; i--)
                {
                    if (_pending[i].RetireAfter > _currentFrame)
                        continue;

                    ready ??= [];
                    ready.Add(_pending[i].Release);
                    _pending.RemoveAt(i);
                }
            }

            if (ready == null)
                return;

            foreach (var release in ready)
            {
                release();
            }
        }

        /// <summary>
        /// Releases everything immediately. Only valid once the GPU is known idle.
        /// </summary>
        public void Flush()
        {
            List<Entry> remaining;

            lock (_lock)
            {
                remaining = [.. _pending];
                _pending.Clear();
            }

            foreach (var entry in remaining)
            {
                entry.Release();
            }
        }
    }
}
