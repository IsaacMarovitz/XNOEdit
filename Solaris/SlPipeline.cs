using Plume;

namespace Solaris
{
    public sealed unsafe class SlPipeline : IDisposable
    {
        private RenderPipeline* _pipeline;

        internal SlPipeline(RenderPipeline* pipeline, in SlPassSignature signature)
        {
            _pipeline = pipeline;
            Signature = signature;
        }

        public SlPassSignature Signature { get; }

        internal RenderPipeline* Handle => _pipeline;

        public void Dispose()
        {
            if (_pipeline == null)
                return;

            _pipeline->Dispose();
            _pipeline = null;
        }
    }

    /// <summary>
    /// The attachment configuration a pipeline is valid for.
    /// </summary>
    public readonly record struct SlPassSignature
    {
        public required RenderFormat ColorFormat0 { get; init; }
        public required RenderFormat DepthFormat { get; init; }
        public required uint SampleCount { get; init; }
        public required uint ColorCount { get; init; }

        public static SlPassSignature SingleColor(RenderFormat color, RenderFormat depth = RenderFormat.Unknown, uint sampleCount = 1) => new()
        {
            ColorFormat0 = color,
            DepthFormat = depth,
            SampleCount = sampleCount,
            ColorCount = 1,
        };
    }
}
