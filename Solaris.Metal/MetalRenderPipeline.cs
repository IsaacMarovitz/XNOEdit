using System.Runtime.Versioning;
using SharpMetal.Foundation;
using SharpMetal.Metal;

namespace Solaris.Metal
{
    [SupportedOSPlatform("macos")]
    internal class MetalRenderPipeline : SlRenderPipeline
    {
        internal MTLRenderPipelineState PipelineState { get; }
        internal MTLCullMode CullMode { get; }
        internal MTLWinding FrontFace { get; }
        internal MTLDepthStencilState? DepthStencilState { get; }
        internal MTLPrimitiveType PrimitiveType { get; }

        internal MetalRenderPipeline(MetalDevice device, SlRenderPipelineDescriptor descriptor)
        {
            var shader = (MetalShaderModule)descriptor.Shader;
            var variant = descriptor.Variant ?? new SlPipelineVariantDescriptor();

            // MTL4: use function descriptors, not MTLFunction objects
            var vertexFuncDesc = new MTL4LibraryFunctionDescriptor();
            vertexFuncDesc.Name = descriptor.VertexEntryPoint ?? "vs_main";
            vertexFuncDesc.Library = shader.Library;

            var fragmentFuncDesc = new MTL4LibraryFunctionDescriptor();
            fragmentFuncDesc.Name = descriptor.FragmentEntryPoint ?? "fs_main";
            fragmentFuncDesc.Library = shader.Library;

            // MTL4: no vertex descriptor — shaders do manual vertex fetch
            var pipelineDesc = new MTL4RenderPipelineDescriptor();
            pipelineDesc.VertexFunctionDescriptor = vertexFuncDesc;
            pipelineDesc.FragmentFunctionDescriptor = fragmentFuncDesc;

            // Color attachment
            var colorAttachment = pipelineDesc.ColorAttachments.Object(0);
            colorAttachment.PixelFormat = descriptor.ColorFormat.Convert();

            if (descriptor.BlendState.HasValue)
            {
                var blend = descriptor.BlendState.Value;
                colorAttachment.BlendingState = MTL4BlendState.Enabled;
                colorAttachment.RgbBlendOperation = blend.Color.Operation.Convert();
                colorAttachment.SourceRGBBlendFactor = blend.Color.SrcFactor.Convert();
                colorAttachment.DestinationRGBBlendFactor = blend.Color.DstFactor.Convert();
                colorAttachment.AlphaBlendOperation = blend.Alpha.Operation.Convert();
                colorAttachment.SourceAlphaBlendFactor = blend.Alpha.SrcFactor.Convert();
                colorAttachment.DestinationAlphaBlendFactor = blend.Alpha.DstFactor.Convert();
            }

            pipelineDesc.ColorAttachments.SetObject(colorAttachment, 0);

            // Depth — MTL4 render pipeline descriptors don't have depth pixel format;
            // it's inferred from the render pass descriptor at draw time

            // Create via MTL4Compiler
            var error = new NSError(IntPtr.Zero);
            PipelineState = device.Compiler.NewRenderPipelineState(
                pipelineDesc, new MTL4CompilerTaskOptions(), ref error);

            if (error != IntPtr.Zero || PipelineState.NativePtr == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to create render pipeline: {error.LocalizedDescription}");

            pipelineDesc.Dispose();
            vertexFuncDesc.Dispose();
            fragmentFuncDesc.Dispose();

            // Dynamic state
            CullMode = variant.CullMode.Convert();
            FrontFace = variant.FrontFace.Convert();
            PrimitiveType = variant.Topology.Convert();

            // Depth/stencil state (set on encoder, not pipeline)
            if (descriptor.DepthFormat.HasValue)
            {
                var depthDesc = new MTLDepthStencilDescriptor();
                depthDesc.DepthCompareFunction = variant.DepthCompare.Convert();
                depthDesc.IsDepthWriteEnabled = variant.DepthWrite;

                DepthStencilState = device.Device.NewDepthStencilState(depthDesc);
                depthDesc.Dispose();
            }
        }

        public override void Dispose()
        {
            if (PipelineState.NativePtr != IntPtr.Zero)
                PipelineState.Dispose();
            if (DepthStencilState?.NativePtr != IntPtr.Zero)
                DepthStencilState?.Dispose();
        }
    }
}
