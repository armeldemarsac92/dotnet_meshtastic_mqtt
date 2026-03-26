using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Application.Abstractions.Collector;

public interface ITopologyReadAdapter
{
    Task<IReadOnlyCollection<NodeSummary>> GetTopologyNodesAsync(
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorMapLinkSummary>> GetTopologyLinksAsync(
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);
}
