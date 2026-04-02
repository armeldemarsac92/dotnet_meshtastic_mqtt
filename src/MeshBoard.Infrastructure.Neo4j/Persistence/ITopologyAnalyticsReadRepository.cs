namespace MeshBoard.Infrastructure.Neo4j.Persistence;

public interface ITopologyAnalyticsReadRepository
{
    Task<IReadOnlyCollection<TopologyNodeAnalytics>> GetNodeAnalyticsAsync(
        string? brokerServer,
        DateTimeOffset notBeforeUtc,
        int maxNodes,
        CancellationToken cancellationToken = default);
}

public sealed class TopologyNodeAnalytics
{
    public required string NodeId { get; init; }

    public int Degree { get; init; }

    public long? ComponentId { get; init; }

    public bool IsBridgeNode { get; init; }
}
