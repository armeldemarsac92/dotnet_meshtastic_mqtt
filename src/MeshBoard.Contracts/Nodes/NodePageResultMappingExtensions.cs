namespace MeshBoard.Contracts.Nodes;

public static class NodePageResultMappingExtensions
{
    public static NodePageResult ToNodePageResult(
        this IReadOnlyCollection<NodeSummary> items,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        return new NodePageResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }
}
