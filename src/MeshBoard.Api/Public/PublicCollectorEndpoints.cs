using MeshBoard.Application.Services;
using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Collector;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Public;

internal static class PublicCollectorEndpoints
{
    private const string Tags = "PublicCollector";

    public static IEndpointRouteBuilder MapPublicCollectorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ApiRoutes.PublicCollector.GetServers, GetObservedServers)
            .WithName("GetCollectorServers")
            .Produces<IReadOnlyCollection<CollectorServerSummary>>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetChannels, GetObservedChannels)
            .WithName("GetCollectorChannels")
            .Produces<IReadOnlyCollection<CollectorChannelSummary>>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetSnapshot, GetSnapshot)
            .WithName("GetCollectorSnapshot")
            .Produces<CollectorMapSnapshot>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetChannelPackets, GetChannelPacketStats)
            .WithName("GetCollectorChannelPackets")
            .Produces<CollectorChannelPacketStatsSnapshot>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetNodePackets, GetNodePacketStats)
            .WithName("GetCollectorNodePackets")
            .Produces<CollectorNodePacketStatsSnapshot>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetNeighborLinks, GetNeighborLinkStats)
            .WithName("GetCollectorNeighborLinks")
            .Produces<CollectorNeighborLinkStatsSnapshot>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetOverview, GetOverview)
            .WithName("GetCollectorOverview")
            .Produces<CollectorOverviewSnapshot>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetNodes, GetNodes)
            .WithName("GetCollectorNodes")
            .Produces<CollectorNodePage>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetChannelsPage, GetChannelsPage)
            .WithName("GetCollectorChannelsPage")
            .Produces<CollectorChannelPage>()
            .WithTags(Tags);

        endpoints.MapGet(ApiRoutes.PublicCollector.GetTopology, GetTopology)
            .WithName("GetCollectorTopology")
            .Produces<CollectorTopologySnapshot>()
            .WithTags(Tags);

        return endpoints;
    }

    private static async Task<IResult> GetObservedServers(
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var servers = await collectorReadService.GetObservedServers(cancellationToken);
        return Results.Ok(servers);
    }

    private static async Task<IResult> GetObservedChannels(
        [AsParameters] CollectorMapRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var channels = await collectorReadService.GetObservedChannels(
            request.ToCollectorMapQuery(),
            cancellationToken);

        return Results.Ok(channels);
    }

    private static async Task<IResult> GetSnapshot(
        [AsParameters] CollectorMapRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var snapshot = await collectorReadService.GetMapSnapshot(
            request.ToCollectorMapQuery(),
            cancellationToken);

        return Results.Ok(snapshot);
    }

    private static async Task<IResult> GetChannelPacketStats(
        [AsParameters] CollectorPacketStatsRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var snapshot = await collectorReadService.GetChannelPacketStats(
            request.ToCollectorPacketStatsQuery(),
            cancellationToken);

        return Results.Ok(snapshot);
    }

    private static async Task<IResult> GetNodePacketStats(
        [AsParameters] CollectorNodePacketStatsRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var snapshot = await collectorReadService.GetNodePacketStats(
            request.ToCollectorPacketStatsQuery(),
            cancellationToken);

        return Results.Ok(snapshot);
    }

    private static async Task<IResult> GetNeighborLinkStats(
        [AsParameters] CollectorNeighborLinkStatsRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var snapshot = await collectorReadService.GetNeighborLinkStats(
            request.ToCollectorNeighborLinkStatsQuery(),
            cancellationToken);

        return Results.Ok(snapshot);
    }

    private static async Task<IResult> GetOverview(
        [AsParameters] CollectorOverviewRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var snapshot = await collectorReadService.GetOverviewSnapshot(
            request.ToCollectorOverviewQuery(),
            cancellationToken);

        return Results.Ok(snapshot);
    }

    private static async Task<IResult> GetNodes(
        [AsParameters] CollectorNodePageRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var result = await collectorReadService.GetNodePage(
            request.ToCollectorNodePageQuery(),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetChannelsPage(
        [AsParameters] CollectorChannelPageRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var result = await collectorReadService.GetChannelPage(
            request.ToCollectorChannelPageQuery(),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetTopology(
        [AsParameters] CollectorTopologyRequest request,
        ICollectorReadService collectorReadService,
        CancellationToken cancellationToken)
    {
        var snapshot = await collectorReadService.GetTopologySnapshot(
            request.ToCollectorTopologyQuery(),
            cancellationToken);

        return Results.Ok(snapshot);
    }
}
