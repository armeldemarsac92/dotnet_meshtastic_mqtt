using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Collector;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IPublicCollectorApi
{
    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.Servers)]
    Task<IApiResponse<IReadOnlyCollection<CollectorServerSummary>>> GetObservedServersAsync(CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.Channels)]
    Task<IApiResponse<IReadOnlyCollection<CollectorChannelSummary>>> GetObservedChannelsAsync(
        [Query] CollectorMapQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.Snapshot)]
    Task<IApiResponse<CollectorMapSnapshot>> GetSnapshotAsync(
        [Query] CollectorMapQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.StatsChannelPackets)]
    Task<IApiResponse<CollectorChannelPacketStatsSnapshot>> GetChannelPacketStatsAsync(
        [Query] CollectorPacketStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.StatsNodePackets)]
    Task<IApiResponse<CollectorNodePacketStatsSnapshot>> GetNodePacketStatsAsync(
        [Query] CollectorPacketStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.StatsNeighborLinks)]
    Task<IApiResponse<CollectorNeighborLinkStatsSnapshot>> GetNeighborLinkStatsAsync(
        [Query] CollectorNeighborLinkStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.Topology)]
    Task<IApiResponse<CollectorTopologySnapshot>> GetTopologyAsync(
        [Query] CollectorTopologyQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.Overview)]
    Task<IApiResponse<CollectorOverviewSnapshot>> GetOverviewAsync(
        [Query] CollectorOverviewQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.Nodes)]
    Task<IApiResponse<CollectorNodePage>> GetNodePageAsync(
        [Query] CollectorNodePageQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.Group + ApiRoutes.PublicCollector.ChannelsPage)]
    Task<IApiResponse<CollectorChannelPage>> GetChannelPageAsync(
        [Query] CollectorChannelPageQuery query,
        CancellationToken cancellationToken = default);
}
