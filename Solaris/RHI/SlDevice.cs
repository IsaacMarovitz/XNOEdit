namespace Solaris.RHI
{
    public abstract class SlDevice : IDisposable
    {
        private const int UniformBufferAlignment = 256;
        public readonly SlTextureFormat SurfaceFormat = SlTextureFormat.Bgra8Unorm;

        public abstract SlQueue GetQueue();

        public abstract SlTexture CreateTexture(SlTextureDescriptor descriptor);

        public abstract SlSampler CreateSampler(SlSamplerDescriptor descriptor);

        /// <summary>
        /// Create a buffer with initial data (for vertex/index buffers)
        /// </summary>
        public abstract SlBuffer<T> CreateBuffer<T>(Span<T> data, SlBufferUsage usage) where T : unmanaged;

        /// <summary>
        /// Create an empty buffer (for uniform buffers)
        /// </summary>
        public abstract SlBuffer<T> CreateBuffer<T>(SlBufferDescriptor descriptor) where T : unmanaged;


        /// <summary>
        /// Create a uniform buffer (convenience method)
        /// </summary>
        public unsafe SlBuffer<T> CreateUniform<T>() where T : unmanaged
        {
            var desc = new SlBufferDescriptor
            {
                Usage = SlBufferUsage.Uniform | SlBufferUsage.CopyDst,
                Size = (ulong)sizeof(T),
                Alignment = UniformBufferAlignment,
            };

            return CreateBuffer<T>(desc);
        }

        public abstract void Dispose();
    }
}
