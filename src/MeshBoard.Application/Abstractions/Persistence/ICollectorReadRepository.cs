using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Nodes;


namespace MeshBoard.Application.Abstractions.Persistence;

public interface ICollectorReadRepository
{
    Task<IReadOnlyCollection<CollectorServerSummary>> GetServersAsync(
        string workspaceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorChannelSummary>> GetChannelsAsync(
        string workspaceId,
        CollectorMapQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NodeSummary>> GetMapNodesAsync(
        string workspaceId,
        CollectorMapQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorMapLinkSummary>> GetMapLinksAsync(
        string workspaceId,
        CollectorMapQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NodeSummary>> GetTopologyNodesAsync(
        string workspaceId,
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorMapLinkSummary>> GetTopologyLinksAsync(
        string workspaceId,
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorChannelPacketHourlyRollup>> GetChannelPacketRollupsAsync(
        string workspaceId,
        CollectorPacketStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorNodePacketHourlyRollup>> GetNodePacketRollupsAsync(
        string workspaceId,
        CollectorPacketStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorOverviewPacketTypeSummary>> GetChannelPacketTypeTotalsAsync(
        string workspaceId,
        CollectorPacketStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorNeighborLinkHourlyRollup>> GetNeighborLinkRollupsAsync(
        string workspaceId,
        CollectorNeighborLinkStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<int> GetNeighborObservationCountAsync(
        string workspaceId,
        CollectorNeighborLinkStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<CollectorNodePage> GetNodePageAsync(
        CollectorNodePageQuery query,
        CancellationToken cancellationToken = default);

    Task<CollectorChannelPage> GetChannelPageAsync(
        CollectorChannelPageQuery query,
        CancellationToken cancellationToken = default);
}
