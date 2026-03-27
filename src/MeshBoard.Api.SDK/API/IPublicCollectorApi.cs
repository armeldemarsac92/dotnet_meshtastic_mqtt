using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Collector;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IPublicCollectorApi
{
    [Get(ApiRoutes.PublicCollector.GetServers)]
    Task<IApiResponse<IReadOnlyCollection<CollectorServerSummary>>> GetObservedServersAsync(CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetChannels)]
    Task<IApiResponse<IReadOnlyCollection<CollectorChannelSummary>>> GetObservedChannelsAsync(
        [Query] CollectorMapQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetSnapshot)]
    Task<IApiResponse<CollectorMapSnapshot>> GetSnapshotAsync(
        [Query] CollectorMapQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetChannelPackets)]
    Task<IApiResponse<CollectorChannelPacketStatsSnapshot>> GetChannelPacketStatsAsync(
        [Query] CollectorPacketStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetNodePackets)]
    Task<IApiResponse<CollectorNodePacketStatsSnapshot>> GetNodePacketStatsAsync(
        [Query] CollectorPacketStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetNeighborLinks)]
    Task<IApiResponse<CollectorNeighborLinkStatsSnapshot>> GetNeighborLinkStatsAsync(
        [Query] CollectorNeighborLinkStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetTopology)]
    Task<IApiResponse<CollectorTopologySnapshot>> GetTopologyAsync(
        [Query] CollectorTopologyQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetOverview)]
    Task<IApiResponse<CollectorOverviewSnapshot>> GetOverviewAsync(
        [Query] CollectorOverviewQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetNodes)]
    Task<IApiResponse<CollectorNodePage>> GetNodePageAsync(
        [Query] CollectorNodePageQuery query,
        CancellationToken cancellationToken = default);

    [Get(ApiRoutes.PublicCollector.GetChannelsPage)]
    Task<IApiResponse<CollectorChannelPage>> GetChannelPageAsync(
        [Query] CollectorChannelPageQuery query,
        CancellationToken cancellationToken = default);
}
