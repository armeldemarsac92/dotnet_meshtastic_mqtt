namespace MeshBoard.Contracts.Nodes;

public sealed class NodeQuery
{
    public string SearchText { get; set; } = string.Empty;

    public bool OnlyFavorites { get; set; }

    public bool OnlyWithLocation { get; set; }

    public bool OnlyWithTelemetry { get; set; }

    public NodeSortOption SortBy { get; set; } = NodeSortOption.LastHeardDesc;
}
