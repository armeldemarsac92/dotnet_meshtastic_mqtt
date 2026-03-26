namespace MeshBoard.Contracts.Collector;

public sealed class CollectorTopologyComponentSummary
{
    public int ComponentIndex { get; set; }

    public int NodeCount { get; set; }

    public int LinkCount { get; set; }

    public IReadOnlyCollection<string> SampleNodeIds { get; set; } = [];
}
