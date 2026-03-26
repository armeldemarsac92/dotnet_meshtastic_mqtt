namespace MeshBoard.Contracts.Collector;

public sealed class CollectorChannelPageQuery
{
    public string? SearchText { get; set; }

    public CollectorChannelSortOption SortBy { get; set; } = CollectorChannelSortOption.LastObservedDesc;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
