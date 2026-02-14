using Silk.NET.WebGPU;
using Solaris.RHI;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Solaris.Wgpu
{
    internal unsafe class WgpuRenderPass : SlRenderPass
    {
        private readonly WgpuDevice _device;
        internal RenderPassEncoder* Handle { get; }

        public WgpuRenderPass(WgpuDevice device, RenderPassEncoder* handle)
        {
            _device = device;
            Handle = handle;
        }

        public override void SetPipeline(SlRenderPipeline pipeline)
        {
            var wgpuPipeline = (WgpuRenderPipeline)pipeline;
           _device.Wgpu.RenderPassEncoderSetPipeline(Handle, wgpuPipeline.Handle);
        }

        public override void SetVertexBuffer(uint slot, SlBuffer<byte> buffer, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetVertexBuffer(
                Handle, slot, (Buffer*)buffer.GetHandle(), offset, buffer.Size);
        }

        public override void SetVertexBuffer<T>(uint slot, SlBuffer<T> buffer, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetVertexBuffer(
                Handle, slot, (Buffer*)buffer.GetHandle(), offset, buffer.Size);
        }

        public override void SetIndexBuffer(SlBuffer<byte> buffer, SlIndexFormat format, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetIndexBuffer(
                Handle, (Buffer*)buffer.GetHandle(), format.Convert(), offset, buffer.Size);
        }

        public override void SetIndexBuffer<T>(SlBuffer<T> buffer, SlIndexFormat format, ulong offset = 0)
        {
           _device.Wgpu.RenderPassEncoderSetIndexBuffer(
                Handle, (Buffer*)buffer.GetHandle(), format.Convert(), offset, buffer.Size);
        }

        public override void SetBindGroup(uint index, SlBindGroup group)
        {
            var wgpuGroup = (WgpuBindGroup)group;
            uint dynamicOffsets = 0;
           _device.Wgpu.RenderPassEncoderSetBindGroup(
                Handle, index, wgpuGroup.Handle, 0, in dynamicOffsets);
        }

        public override void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
        {
           _device.Wgpu.RenderPassEncoderSetViewport(Handle, x, y, width, height, minDepth, maxDepth);
        }

        public override void SetScissorRect(uint x, uint y, uint width, uint height)
        {
           _device.Wgpu.RenderPassEncoderSetScissorRect(Handle, x, y, width, height);
        }

        public override void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        {
           _device.Wgpu.RenderPassEncoderDraw(Handle, vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public override void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
        {
           _device.Wgpu.RenderPassEncoderDrawIndexed(Handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);
        }

        public override void End()
        {
           _device.Wgpu.RenderPassEncoderEnd(Handle);
        }

        public override void Dispose()
        {
            if (Handle != null)
               _device.Wgpu.RenderPassEncoderRelease(Handle);
        }
    }
}
