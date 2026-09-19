namespace Solaris.Graph
{
    /// <summary>
    /// Decides the order passes execute in. Barrier emission is driven entirely by the
    /// order this returns.
    /// </summary>
    internal interface ISlPassScheduler
    {
        /// <summary>
        /// Returns pass indices in execution order. May omit passes (culling) and may
        /// reorder them, provided every dependency in <see cref="SlPassNode.Dependencies"/>
        /// precedes its dependents.
        /// </summary>
        IReadOnlyList<int> Schedule(IReadOnlyList<SlPassNode> passes);
    }

    /// <summary>
    /// Executes passes in declaration order.
    ///
    /// Naive implementation that does no culling or reordering.
    /// </summary>
    internal sealed class SlLinearScheduler : ISlPassScheduler
    {
        private readonly List<int> _order = [];

        public IReadOnlyList<int> Schedule(IReadOnlyList<SlPassNode> passes)
        {
            _order.Clear();

            for (var i = 0; i < passes.Count; i++)
            {
                if (passes[i].Body != null)
                {
                    _order.Add(i);
                }
            }

            return _order;
        }
    }
}
