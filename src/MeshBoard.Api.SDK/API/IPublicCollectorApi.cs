using MeshBoard.Contracts.Collector;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IPublicCollectorApi
{
    [Get("/api/public/collector/servers")]
    Task<IApiResponse<IReadOnlyCollection<CollectorServerSummary>>> GetObservedServersAsync(CancellationToken cancellationToken = default);

    [Get("/api/public/collector/channels")]
    Task<IApiResponse<IReadOnlyCollection<CollectorChannelSummary>>> GetObservedChannelsAsync(
        [Query] CollectorMapQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/snapshot")]
    Task<IApiResponse<CollectorMapSnapshot>> GetSnapshotAsync(
        [Query] CollectorMapQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/stats/channel-packets")]
    Task<IApiResponse<CollectorChannelPacketStatsSnapshot>> GetChannelPacketStatsAsync(
        [Query] CollectorPacketStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/stats/node-packets")]
    Task<IApiResponse<CollectorNodePacketStatsSnapshot>> GetNodePacketStatsAsync(
        [Query] CollectorPacketStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/stats/neighbor-links")]
    Task<IApiResponse<CollectorNeighborLinkStatsSnapshot>> GetNeighborLinkStatsAsync(
        [Query] CollectorNeighborLinkStatsQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/topology")]
    Task<IApiResponse<CollectorTopologySnapshot>> GetTopologyAsync(
        [Query] CollectorTopologyQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/overview")]
    Task<IApiResponse<CollectorOverviewSnapshot>> GetOverviewAsync(
        [Query] CollectorOverviewQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/nodes")]
    Task<IApiResponse<CollectorNodePage>> GetNodePageAsync(
        [Query] CollectorNodePageQuery query,
        CancellationToken cancellationToken = default);

    [Get("/api/public/collector/channels-page")]
    Task<IApiResponse<CollectorChannelPage>> GetChannelPageAsync(
        [Query] CollectorChannelPageQuery query,
        CancellationToken cancellationToken = default);
}
