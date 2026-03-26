namespace MeshBoard.Contracts.Collector;

public sealed class CollectorTopologyNodeSummary
{
    public string NodeId { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? LongName { get; set; }

    public int Degree { get; set; }

    public int ComponentSize { get; set; }
}
