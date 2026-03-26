namespace MeshBoard.Contracts.Collector;

public sealed class CollectorNodePageQuery
{
    public string? SearchText { get; set; }

    public CollectorNodeSortOption SortBy { get; set; } = CollectorNodeSortOption.LastHeardDesc;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
