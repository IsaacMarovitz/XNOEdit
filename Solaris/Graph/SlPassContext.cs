using System.Runtime.CompilerServices;
using Plume;

namespace Solaris.Graph
{
    public sealed unsafe class SlPassContext
    {
        private readonly SlFrame _frame;
        private readonly RenderCommandList* _commandList;

        private float _viewportX;
        private float _viewportY;
        private float _viewportWidth;
        private float _viewportHeight;

        internal SlPassContext(
            SlFrame frame, RenderCommandList* commandList, uint width, uint height, in SlPassSignature signature)
        {
            _frame = frame;
            _commandList = commandList;
            Width = width;
            Height = height;
            Signature = signature;

            _viewportWidth = width;
            _viewportHeight = height;
        }

        public uint Width { get; }

        public uint Height { get; }

        /// <summary>
        /// The attachment configuration of this pass. Materials key their pipeline cache
        /// on it, since plume bakes target formats into the pipeline.
        /// </summary>
        public SlPassSignature Signature { get; }

        /// <summary>The frame's upload ring, for transient vertex, index and constant data.</summary>
        public SlUploadRing Ring => _frame.Ring;

        public void SetPipeline(SlPipeline pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            _commandList->SetPipeline(pipeline.Handle);
        }

        public void SetViewport(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f)
        {
            _viewportX = x;
            _viewportY = y;
            _viewportWidth = width;
            _viewportHeight = height;

            _commandList->SetViewports(new RenderViewport(x, y, width, height, minDepth, maxDepth));
        }

        public void SetViewportDepthRange(float minDepth, float maxDepth)
        {
            _commandList->SetViewports(
                new RenderViewport(_viewportX, _viewportY, _viewportWidth, _viewportHeight, minDepth, maxDepth));
        }

        public void SetScissor(int left, int top, int right, int bottom)
        {
            _commandList->SetScissors(new RenderRect(left, top, right, bottom));
        }

        public void SetVertexBuffer(uint slot, SlBufferView buffer, uint stride)
        {
            var view = new RenderVertexBufferView(buffer.Reference, checked((uint)buffer.Size));
            var inputSlot = new RenderInputSlot(slot, stride);

            _commandList->SetVertexBuffers(slot, &view, 1, &inputSlot);
        }

        public void SetIndexBuffer(SlBufferView buffer, RenderFormat format = RenderFormat.R16Uint)
        {
            var view = new RenderIndexBufferView(buffer.Reference, checked((uint)buffer.Size), format);
            _commandList->SetIndexBuffer(&view);
        }

        public void SetDepthBias(float constant, float clamp, float slopeScaled)
        {
            _commandList->SetDepthBias(constant, clamp, slopeScaled);
        }

        public void PushConstants<T>(in T data, uint offset = 0) where T : unmanaged
        {
            var size = (uint)Unsafe.SizeOf<T>();

            if (offset + size > SlGlobalLayout.PushConstantSize)
            {
                throw new ArgumentException(
                    $"{typeof(T).Name} at offset {offset} is {size} bytes, past the {SlGlobalLayout.PushConstantSize} byte block. " +
                    "Use UploadConstants for larger data.", nameof(data));
            }

            fixed (T* pointer = &data)
            {
                _commandList->SetGraphicsPushConstants(0, pointer, offset, size);
            }
        }

        public uint UploadConstants<T>(in T data) where T : unmanaged
        {
            return Ring.Write(in data).ConstantOffset;
        }

        public SlBufferView UploadVertices<T>(ReadOnlySpan<T> vertices) where T : unmanaged
        {
            return Ring.Write(vertices).View;
        }

        public SlBufferView UploadIndices<T>(ReadOnlySpan<T> indices) where T : unmanaged
        {
            return Ring.Write(indices).View;
        }

        public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        {
            _commandList->DrawInstanced(vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
        {
            _commandList->DrawIndexedInstanced(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
        }
    }
}
