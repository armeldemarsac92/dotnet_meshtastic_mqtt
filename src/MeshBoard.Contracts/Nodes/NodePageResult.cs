namespace MeshBoard.Contracts.Nodes;

public sealed class NodePageResult
{
    public IReadOnlyCollection<NodeSummary> Items { get; set; } = [];

    public int TotalCount { get; set; }
}
