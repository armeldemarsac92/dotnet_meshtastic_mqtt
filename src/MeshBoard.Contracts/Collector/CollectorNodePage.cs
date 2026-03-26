using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Contracts.Collector;

public sealed class CollectorNodePage
{
    public IReadOnlyList<NodeSummary> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
