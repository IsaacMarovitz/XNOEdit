using System.Runtime.InteropServices;
using Plume;

namespace Solaris
{
    /// <summary>
    /// Because there is only one layout, nothing in Solaris or above ever builds a
    /// descriptor set layout, and per-draw binding is push constants only.
    /// </summary>
    public sealed unsafe class SlGlobalLayout : IDisposable
    {
        public const uint TextureSet2D = 0;
        public const uint TextureSet2DArray = 1;
        public const uint TextureSetCube = 2;
        public const uint SamplerSet = 3;
        public const uint ConstantSet = 4;
        public const uint SetCount = 5;

        /// <summary>
        /// Vulkan guarantees only 128 bytes of push constant space, so that is the budget.
        /// Anything larger belongs in the per-frame upload ring, addressed by an offset
        /// pushed through here.
        /// </summary>
        public const uint PushConstantSize = 128;

        /// <summary>
        /// Layout ceiling for the boundless ranges. This bounds the descriptor set
        /// layout only; the actual allocation is <see cref="SlBindlessTables"/>'s
        /// current capacity, which grows on demand.
        /// </summary>
        public const uint TextureTableCeiling = 1 << 20;

        public const uint SamplerTableCeiling = 4096;

        private RenderPipelineLayout* _layout;

        // The retained set descriptions hold pointers to these, so they must outlive the
        // constructor, the table reuses them every time it allocates a larger set.
        private RenderDescriptorRange* _textureRange;
        private RenderDescriptorRange* _samplerRange;
        private RenderDescriptorRange* _constantRange;

        internal SlGlobalLayout(RenderDevice* device, uint textureCapacity, uint samplerCapacity)
        {
            _textureRange = (RenderDescriptorRange*)NativeMemory.Alloc((nuint)sizeof(RenderDescriptorRange));
            _samplerRange = (RenderDescriptorRange*)NativeMemory.Alloc((nuint)sizeof(RenderDescriptorRange));
            _constantRange = (RenderDescriptorRange*)NativeMemory.Alloc((nuint)sizeof(RenderDescriptorRange));

            *_textureRange = new RenderDescriptorRange(RenderDescriptorRangeType.Texture, 0, TextureTableCeiling);
            *_samplerRange = new RenderDescriptorRange(RenderDescriptorRangeType.Sampler, 0, SamplerTableCeiling);
            *_constantRange = new RenderDescriptorRange(RenderDescriptorRangeType.StructuredBuffer, 0, 1);

            var textureRange = _textureRange;
            var samplerRange = _samplerRange;
            var constantRange = _constantRange;

            var pushConstant = new RenderPushConstantRange(
                binding: 0,
                set: 0,
                offset: 0,
                size: PushConstantSize,
                stageFlags: RenderShaderStageFlags.Vertex | RenderShaderStageFlags.Pixel);

            var setDescs = stackalloc RenderDescriptorSetDesc[(int)SetCount];

            setDescs[TextureSet2D] = new RenderDescriptorSetDesc(textureRange, 1, true, textureCapacity);
            setDescs[TextureSet2DArray] = setDescs[TextureSet2D];
            setDescs[TextureSetCube] = setDescs[TextureSet2D];
            setDescs[SamplerSet] = new RenderDescriptorSetDesc(samplerRange, 1, true, samplerCapacity);
            setDescs[ConstantSet] = new RenderDescriptorSetDesc(constantRange, 1);

            var layoutDesc = new RenderPipelineLayoutDesc(&pushConstant, 1, setDescs, SetCount)
            {
                AllowInputLayout = true,
            };

            _layout = device->CreatePipelineLayout(&layoutDesc);

            if (_layout == null)
                throw new InvalidOperationException("Failed to create the global pipeline layout.");

            TextureSetDesc = setDescs[TextureSet2D];
            SamplerSetDesc = setDescs[SamplerSet];
            ConstantSetDesc = setDescs[ConstantSet];
        }

        internal RenderPipelineLayout* Handle => _layout;

        internal RenderDescriptorSetDesc TextureSetDesc { get; }

        internal RenderDescriptorSetDesc SamplerSetDesc { get; }

        internal RenderDescriptorSetDesc ConstantSetDesc { get; }

        public void Dispose()
        {
            if (_layout != null)
            {
                _layout->Dispose();
                _layout = null;
            }

            if (_textureRange != null)
            {
                NativeMemory.Free(_textureRange);
                _textureRange = null;
            }

            if (_samplerRange != null)
            {
                NativeMemory.Free(_samplerRange);
                _samplerRange = null;
            }

            if (_constantRange != null)
            {
                NativeMemory.Free(_constantRange);
                _constantRange = null;
            }
        }
    }
}
