namespace MeshBoard.Contracts.Collector;

public sealed class CollectorChannelPage
{
    public IReadOnlyList<CollectorChannelSummary> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
