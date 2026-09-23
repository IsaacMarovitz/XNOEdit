using System.Numerics;
using Hexa.NET.ImGui;
using Solaris.Graph;

namespace XNOEdit.Panels
{
    public struct PerformanceSample
    {
        public float FrameTime;
        public long ManagedMemory;
        public long WorkingSet;
        public SlFrameGraph.SlFrameStatistics Graph;
        public ulong StagingFlushed;
        public ulong StagingCapacity;
        public int PendingRetirements;
        public uint BindlessTextures;
    }

    public class PerformancePanel
    {
        public const string Name = "Performance";

        private readonly PerformanceHistory _frameTime = new("Frame Time", "ms");
        private readonly PerformanceHistory _managedMemory = new("Managed Heap", "MiB");
        private readonly PerformanceHistory _workingSet = new("Working Set", "MiB");
        private readonly PerformanceHistory _gpuWait = new("GPU Wait", "ms");
        private readonly PerformanceHistory _uploadRing = new("Upload Ring", "MiB");
        private readonly PerformanceHistory _staging = new("Staging Flush", "MiB");
        private readonly PerformanceHistory _retirementQueue = new("Retirement Queue", "items");
        private readonly PerformanceHistory _graphResrouces = new("Graph Resrouces", "items");
        private readonly PerformanceHistory _bindlessSlots = new("Bindless Slots", "items");
        private float _stagingCapacity;
        private float _uploadRingCapacity;

        public void Record(in PerformanceSample sample)
        {
            _frameTime.Add(sample.FrameTime * 1000.0f);
            _managedMemory.Add(ToMiB(sample.ManagedMemory));
            _workingSet.Add(ToMiB(sample.WorkingSet));
            _gpuWait.Add((float)sample.Graph.FenceWait.TotalMilliseconds);
            _uploadRing.Add(ToMiB(sample.Graph.RingBytesUsed));
            _uploadRingCapacity = ToMiB(sample.Graph.RingCapacity);
            _staging.Add(ToMiB(sample.StagingFlushed));
            _stagingCapacity = ToMiB(sample.StagingCapacity);
            _retirementQueue.Add(sample.PendingRetirements);
            _graphResrouces.Add(sample.Graph.TrackedResources);
            _bindlessSlots.Add(sample.BindlessTextures);
        }

        public void Render()
        {
            ImGui.Begin(Name);

            ImGui.SeparatorText("Frame");
            _frameTime.Plot();
            _gpuWait.Plot();

            ImGui.SeparatorText("Uploads");
            _uploadRing.Plot(_uploadRingCapacity);
            _staging.Plot(_stagingCapacity);

            ImGui.SeparatorText("Lifetimes");
            _retirementQueue.Plot();
            _graphResrouces.Plot();
            _bindlessSlots.Plot();

            ImGui.SeparatorText("Memory");
            _managedMemory.Plot();
            _workingSet.Plot();

            ImGui.End();
        }

        private static float ToMiB(double bytes) => (float)(bytes / (1024 * 1024));
    }

    /// <summary>A rolling window of samples, plotted oldest to newest.</summary>
    internal sealed class PerformanceHistory(string label, string unit)
    {
        private const int Length = 240;
        private const float LabelPadding = 4.0f;

        private readonly float[] _values = new float[Length];
        private int _next;

        public void Add(float value)
        {
            _values[_next] = value;
            _next = (_next + 1) % Length;
        }

        public void Plot(float? ceiling = null)
        {
            var latest = _values[(_next + Length - 1) % Length];
            var peak = _values.Max();

            ImGui.TextUnformatted($"{label}: {latest:0.00} {unit} (peak {peak:0.00})");
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + LabelPadding);

            ImGuiInterop.PlotLines($"##{label}", _values, _next, 0.0f, ceiling ?? peak * 1.1f, new Vector2(-1, 50));
        }
    }
}
