using Silk.NET.WebGPU;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuRenderPass : SlRenderPass
    {
        private readonly WgpuDevice _device;
        private readonly RenderPassEncoder* _handle;

        public static implicit operator RenderPassEncoder*(WgpuRenderPass renderPass) => renderPass._handle;

        public WgpuRenderPass(WgpuDevice device, RenderPassEncoder* handle)
        {
            _device = device;
            _handle = handle;
        }

        public override void SetPipeline(SlRenderPipeline pipeline)
        {
            var wgpuPipeline = (WgpuRenderPipeline)pipeline;
           _device.Wgpu.RenderPassEncoderSetPipeline(_handle, wgpuPipeline);
        }

        public override void SetVertexBuffer(uint slot, SlBuffer<byte> buffer, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetVertexBuffer(
                _handle, slot, (buffer as WgpuBuffer<byte>)!, offset, buffer.Size);
        }

        public override void SetVertexBuffer<T>(uint slot, SlBuffer<T> buffer, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetVertexBuffer(
                _handle, slot, (buffer as WgpuBuffer<T>)!, offset, buffer.Size);
        }

        public override void SetIndexBuffer(SlBuffer<byte> buffer, SlIndexFormat format, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetIndexBuffer(
                _handle, (buffer as WgpuBuffer<byte>)!, format.Convert(), offset, buffer.Size);
        }

        public override void SetIndexBuffer<T>(SlBuffer<T> buffer, SlIndexFormat format, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetIndexBuffer(
                _handle, (buffer as WgpuBuffer<T>)!, format.Convert(), offset, buffer.Size);
        }

        public override void SetBindGroup(uint index, SlBindGroup group)
        {
            var wgpuGroup = (WgpuBindGroup)group;
            uint dynamicOffsets = 0;
           _device.Wgpu.RenderPassEncoderSetBindGroup(
                _handle, index, wgpuGroup, 0, in dynamicOffsets);
        }

        public override void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
        {
           _device.Wgpu.RenderPassEncoderSetViewport(_handle, x, y, width, height, minDepth, maxDepth);
        }

        public override void SetScissorRect(uint x, uint y, uint width, uint height)
        {
           _device.Wgpu.RenderPassEncoderSetScissorRect(_handle, x, y, width, height);
        }

        public override void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        {
           _device.Wgpu.RenderPassEncoderDraw(_handle, vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public override void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
        {
           _device.Wgpu.RenderPassEncoderDrawIndexed(_handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
        }

        public override void End()
        {
           _device.Wgpu.RenderPassEncoderEnd(_handle);
        }

        public override void Dispose()
        {
            if (_handle != null)
               _device.Wgpu.RenderPassEncoderRelease(_handle);
        }
    }
}
