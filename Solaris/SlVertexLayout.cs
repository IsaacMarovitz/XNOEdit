using System.Runtime.InteropServices;
using System.Text;
using Plume;

namespace Solaris
{
    public enum SlVertexStepMode
    {
        Vertex,
        Instance,
    }

    public readonly record struct SlVertexAttribute(
        string SemanticName,
        uint SemanticIndex,
        uint Location,
        RenderFormat Format,
        uint Offset);

    public readonly record struct SlVertexBufferLayout(
        uint Slot,
        uint Stride,
        SlVertexStepMode StepMode,
        SlVertexAttribute[] Attributes);

    public sealed unsafe class SlVertexLayout : IDisposable
    {
        private readonly RenderInputSlot[] _slots;
        private readonly RenderInputElement[] _elements;
        private readonly List<nint> _nameAllocations = [];
        private bool _disposed;

        internal ReadOnlySpan<RenderInputSlot> Slots => _slots;
        internal ReadOnlySpan<RenderInputElement> Elements => _elements;

        public SlVertexLayout(params SlVertexBufferLayout[] layouts)
        {
            ArgumentNullException.ThrowIfNull(layouts);

            _slots = new RenderInputSlot[layouts.Length];

            var elements = new List<RenderInputElement>();

            for (var i = 0; i < layouts.Length; i++)
            {
                var layout = layouts[i];

                _slots[i] = new RenderInputSlot(
                    layout.Slot,
                    layout.Stride,
                    layout.StepMode == SlVertexStepMode.Instance
                        ? RenderInputSlotClassification.PerInstanceData
                        : RenderInputSlotClassification.PerVertexData);

                foreach (var attribute in layout.Attributes)
                {
                    elements.Add(new RenderInputElement(
                        AllocateName(attribute.SemanticName),
                        attribute.SemanticIndex,
                        attribute.Location,
                        attribute.Format,
                        layout.Slot,
                        attribute.Offset));
                }
            }

            _elements = [.. elements];
        }

        private sbyte* AllocateName(string name)
        {
            var byteCount = Encoding.UTF8.GetByteCount(name);
            var buffer = (byte*)NativeMemory.Alloc((nuint)byteCount + 1);

            Encoding.UTF8.GetBytes(name, new Span<byte>(buffer, byteCount));
            buffer[byteCount] = 0;

            _nameAllocations.Add((nint)buffer);
            return (sbyte*)buffer;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            foreach (var allocation in _nameAllocations)
            {
                NativeMemory.Free((void*)allocation);
            }

            _nameAllocations.Clear();
        }
    }
}
