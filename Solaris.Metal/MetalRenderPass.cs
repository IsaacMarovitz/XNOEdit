using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalRenderPass : SlRenderPass
    {
        private MTL4RenderCommandEncoder _encoder;
        private MTL4ArgumentTable _vertexArgTable;
        private MTL4ArgumentTable _fragmentArgTable;

        private MetalRenderPipeline? _currentPipeline;

        private ulong _indexBufferGpuAddress;
        private MTLIndexType _indexType;
        private ulong _indexBufferOffset;

        internal MetalRenderPass(MetalDevice device, MTL4RenderCommandEncoder encoder)
        {
            _encoder = encoder;

            var vertexArgDesc = new MTL4ArgumentTableDescriptor();
            vertexArgDesc.MaxBufferBindCount = MetalBindingLayout.MaxBufferBindings;
            vertexArgDesc.MaxTextureBindCount = MetalBindingLayout.MaxTextureBindings;
            vertexArgDesc.MaxSamplerStateBindCount = MetalBindingLayout.MaxSamplerBindings;

            var fragmentArgDesc = new MTL4ArgumentTableDescriptor();
            fragmentArgDesc.MaxBufferBindCount = MetalBindingLayout.MaxBufferBindings;
            fragmentArgDesc.MaxTextureBindCount = MetalBindingLayout.MaxTextureBindings;
            fragmentArgDesc.MaxSamplerStateBindCount = MetalBindingLayout.MaxSamplerBindings;

            var error = new NSError(IntPtr.Zero);
            _vertexArgTable = device.Device.NewArgumentTable(vertexArgDesc, ref error);
            _fragmentArgTable = device.Device.NewArgumentTable(fragmentArgDesc, ref error);

            vertexArgDesc.Dispose();
            fragmentArgDesc.Dispose();
        }

        public override void SetPipeline(SlRenderPipeline pipeline)
        {
            _currentPipeline = (MetalRenderPipeline)pipeline;
            _encoder.SetRenderPipelineState(_currentPipeline.PipelineState);
            _encoder.SetCullMode(_currentPipeline.CullMode);
            _encoder.SetFrontFacingWinding(_currentPipeline.FrontFace);

            if (_currentPipeline.DepthStencilState.HasValue)
                _encoder.SetDepthStencilState(_currentPipeline.DepthStencilState.Value);
        }

        public override void SetVertexBuffer(uint slot, SlBuffer<byte> buffer, ulong offset = 0)
        {
            _vertexArgTable.SetAddress(buffer.GpuAddress + offset, slot);
        }

        public override void SetVertexBuffer<T>(uint slot, SlBuffer<T> buffer, ulong offset = 0)
        {
            _vertexArgTable.SetAddress(buffer.GpuAddress + offset, slot);
        }

        public override void SetIndexBuffer(SlBuffer<byte> buffer, SlIndexFormat format, ulong offset = 0)
        {
            _indexBufferGpuAddress = buffer.GpuAddress;
            _indexType = format.Convert();
            _indexBufferOffset = offset;
        }

        public override void SetIndexBuffer<T>(SlBuffer<T> buffer, SlIndexFormat format, ulong offset = 0)
        {
            _indexBufferGpuAddress = buffer.GpuAddress;
            _indexType = format.Convert();
            _indexBufferOffset = offset;
        }

        public override void SetBindGroup(uint index, SlBindGroup group)
        {
            var metalGroup = (MetalBindGroup)group;

            foreach (ref readonly var res in metalGroup.Resources.AsSpan())
            {
                switch (res.Type)
                {
                    case SlBindingType.Buffer:
                    {
                        var gpuAddress = res.BufferSource.GpuAddress + res.BufferOffset;

                        var idx = (UIntPtr)MetalBindingLayout.BufferIndex(index, res.Binding);
                        if (res.Visibility.HasFlag(SlShaderStage.Vertex))
                            _vertexArgTable.SetAddress(gpuAddress, idx);
                        if (res.Visibility.HasFlag(SlShaderStage.Fragment))
                            _fragmentArgTable.SetAddress(gpuAddress, idx);
                        break;
                    }
                    case SlBindingType.Texture:
                    {
                        var idx = (UIntPtr)MetalBindingLayout.TextureIndex(index, res.Binding);
                        if (res.Visibility.HasFlag(SlShaderStage.Vertex))
                            _vertexArgTable.SetTexture(res.TextureResourceId, idx);
                        if (res.Visibility.HasFlag(SlShaderStage.Fragment))
                            _fragmentArgTable.SetTexture(res.TextureResourceId, idx);
                        break;
                    }
                    case SlBindingType.Sampler:
                    {
                        var idx = (UIntPtr)MetalBindingLayout.SamplerIndex(index, res.Binding);
                        if (res.Visibility.HasFlag(SlShaderStage.Vertex))
                            _vertexArgTable.SetSamplerState(res.SamplerState.GpuResourceID, idx);
                        if (res.Visibility.HasFlag(SlShaderStage.Fragment))
                            _fragmentArgTable.SetSamplerState(res.SamplerState.GpuResourceID, idx);
                        break;
                    }
                }
            }
        }

        public override void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
        {
            _encoder.SetViewport(new MTLViewport
            {
                originX = x, originY = y,
                width = width, height = height,
                znear = minDepth, zfar = maxDepth,
            });
        }

        public override void SetScissorRect(uint x, uint y, uint width, uint height)
        {
            _encoder.SetScissorRect(new MTLScissorRect
            {
                x = x, y = y,
                width = width, height = height,
            });
        }

        public override void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        {
            _encoder.SetArgumentTable(_vertexArgTable, MTLRenderStages.RenderStageVertex);
            _encoder.SetArgumentTable(_fragmentArgTable, MTLRenderStages.RenderStageFragment);

            var type = _currentPipeline?.PrimitiveType ?? MTLPrimitiveType.Triangle;
            _encoder.DrawPrimitives(type, firstVertex, vertexCount, instanceCount);
        }

        public override void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
        {
            _encoder.SetArgumentTable(_vertexArgTable, MTLRenderStages.RenderStageVertex);
            _encoder.SetArgumentTable(_fragmentArgTable, MTLRenderStages.RenderStageFragment);

            var type = _currentPipeline?.PrimitiveType ?? MTLPrimitiveType.Triangle;
            var indexSize = _indexType == MTLIndexType.UInt16 ? 2u : 4u;
            var offset = _indexBufferOffset + firstIndex * indexSize;

            _encoder.DrawIndexedPrimitives(
                type,
                indexCount,
                _indexType,
                _indexBufferGpuAddress + (UIntPtr)offset,
                0,
                instanceCount,
                baseVertex,
                firstInstance);
        }

        public override void End()
        {
            _encoder.BarrierAfterStages(MTLStages.StageFragment, MTLStages.StageFragment, MTL4VisibilityOptions.Device);
            _encoder.EndEncoding();
        }

        public override void Dispose()
        {
            if (_encoder.NativePtr != IntPtr.Zero)
                _encoder.Dispose();

            if (_vertexArgTable != IntPtr.Zero)
                _vertexArgTable.Dispose();

            if (_fragmentArgTable != IntPtr.Zero)
                _fragmentArgTable.Dispose();
        }
    }
}
