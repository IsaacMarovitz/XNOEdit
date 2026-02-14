namespace Solaris.RHI
{
    public abstract class SlDevice : IDisposable
    {
        private const int UniformBufferAlignment = 256;

        public abstract SlQueue GetQueue();

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

    public struct SlBufferDescriptor
    {
        public SlBufferUsage Usage;
        public ulong Size;
        public int Alignment;
    }
}
