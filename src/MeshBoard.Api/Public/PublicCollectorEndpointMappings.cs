using MeshBoard.Application.Services;
using MeshBoard.Contracts.Collector;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Public;

internal static class PublicCollectorEndpointMappings
{
    public static IEndpointRouteBuilder MapPublicCollectorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/public/collector");

        group.MapGet(
            "/servers",
            async Task<IResult> (
                ICollectorReadService collectorReadService,
                CancellationToken cancellationToken) =>
            {
                var servers = await collectorReadService.GetObservedServers(cancellationToken);
                return Results.Ok(servers);
            });

        group.MapGet(
            "/channels",
            async Task<IResult> (
                [FromQuery(Name = "serverAddress")] string? serverAddress,
                [FromQuery] string? region,
                [FromQuery] string? channelName,
                [FromQuery] int? activeWithinHours,
                [FromQuery] int? maxNodes,
                [FromQuery] int? maxLinks,
                ICollectorReadService collectorReadService,
                CancellationToken cancellationToken) =>
            {
                var channels = await collectorReadService.GetObservedChannels(
                    CreateQuery(serverAddress, region, channelName, activeWithinHours, maxNodes, maxLinks),
                    cancellationToken);
                return Results.Ok(channels);
            });

        group.MapGet(
            "/snapshot",
            async Task<IResult> (
                [FromQuery(Name = "serverAddress")] string? serverAddress,
                [FromQuery] string? region,
                [FromQuery] string? channelName,
                [FromQuery] int? activeWithinHours,
                [FromQuery] int? maxNodes,
                [FromQuery] int? maxLinks,
                ICollectorReadService collectorReadService,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await collectorReadService.GetMapSnapshot(
                    CreateQuery(serverAddress, region, channelName, activeWithinHours, maxNodes, maxLinks),
                    cancellationToken);
                return Results.Ok(snapshot);
            });

        group.MapGet(
            "/stats/channel-packets",
            async Task<IResult> (
                [FromQuery(Name = "serverAddress")] string? serverAddress,
                [FromQuery] string? region,
                [FromQuery] string? channelName,
                [FromQuery] string? packetType,
                [FromQuery] int? lookbackHours,
                [FromQuery] int? maxRows,
                ICollectorReadService collectorReadService,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await collectorReadService.GetChannelPacketStats(
                    CreatePacketStatsQuery(serverAddress, region, channelName, null, packetType, lookbackHours, maxRows),
                    cancellationToken);
                return Results.Ok(snapshot);
            });

        group.MapGet(
            "/stats/node-packets",
            async Task<IResult> (
                [FromQuery(Name = "serverAddress")] string? serverAddress,
                [FromQuery] string? region,
                [FromQuery] string? channelName,
                [FromQuery] string? nodeId,
                [FromQuery] string? packetType,
                [FromQuery] int? lookbackHours,
                [FromQuery] int? maxRows,
                ICollectorReadService collectorReadService,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await collectorReadService.GetNodePacketStats(
                    CreatePacketStatsQuery(serverAddress, region, channelName, nodeId, packetType, lookbackHours, maxRows),
                    cancellationToken);
                return Results.Ok(snapshot);
            });

        group.MapGet(
            "/stats/neighbor-links",
            async Task<IResult> (
                [FromQuery(Name = "serverAddress")] string? serverAddress,
                [FromQuery] string? region,
                [FromQuery] string? channelName,
                [FromQuery] string? sourceNodeId,
                [FromQuery] string? targetNodeId,
                [FromQuery] int? lookbackHours,
                [FromQuery] int? maxRows,
                ICollectorReadService collectorReadService,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await collectorReadService.GetNeighborLinkStats(
                    CreateNeighborLinkStatsQuery(serverAddress, region, channelName, sourceNodeId, targetNodeId, lookbackHours, maxRows),
                    cancellationToken);
                return Results.Ok(snapshot);
            });

        group.MapGet(
            "/topology",
            async Task<IResult> (
                [FromQuery(Name = "serverAddress")] string? serverAddress,
                [FromQuery] string? region,
                [FromQuery] string? channelName,
                [FromQuery] int? activeWithinHours,
                [FromQuery] int? maxNodes,
                [FromQuery] int? maxLinks,
                [FromQuery] int? topCount,
                ICollectorReadService collectorReadService,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await collectorReadService.GetTopologySnapshot(
                    CreateTopologyQuery(serverAddress, region, channelName, activeWithinHours, maxNodes, maxLinks, topCount),
                    cancellationToken);
                return Results.Ok(snapshot);
            });

        return endpoints;
    }

    private static CollectorMapQuery CreateQuery(
        string? serverAddress,
        string? region,
        string? channelName,
        int? activeWithinHours,
        int? maxNodes,
        int? maxLinks)
    {
        return new CollectorMapQuery
        {
            ServerAddress = serverAddress,
            Region = region,
            ChannelName = channelName,
            ActiveWithinHours = activeWithinHours ?? 24,
            MaxNodes = maxNodes ?? 5_000,
            MaxLinks = maxLinks ?? 10_000
        };
    }

    private static CollectorPacketStatsQuery CreatePacketStatsQuery(
        string? serverAddress,
        string? region,
        string? channelName,
        string? nodeId,
        string? packetType,
        int? lookbackHours,
        int? maxRows)
    {
        return new CollectorPacketStatsQuery
        {
            ServerAddress = serverAddress,
            Region = region,
            ChannelName = channelName,
            NodeId = nodeId,
            PacketType = packetType,
            LookbackHours = lookbackHours ?? 24 * 7,
            MaxRows = maxRows ?? 500
        };
    }

    private static CollectorNeighborLinkStatsQuery CreateNeighborLinkStatsQuery(
        string? serverAddress,
        string? region,
        string? channelName,
        string? sourceNodeId,
        string? targetNodeId,
        int? lookbackHours,
        int? maxRows)
    {
        return new CollectorNeighborLinkStatsQuery
        {
            ServerAddress = serverAddress,
            Region = region,
            ChannelName = channelName,
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            LookbackHours = lookbackHours ?? 24 * 7,
            MaxRows = maxRows ?? 500
        };
    }

    private static CollectorTopologyQuery CreateTopologyQuery(
        string? serverAddress,
        string? region,
        string? channelName,
        int? activeWithinHours,
        int? maxNodes,
        int? maxLinks,
        int? topCount)
    {
        return new CollectorTopologyQuery
        {
            ServerAddress = serverAddress,
            Region = region,
            ChannelName = channelName,
            ActiveWithinHours = activeWithinHours ?? 24,
            MaxNodes = maxNodes ?? 10_000,
            MaxLinks = maxLinks ?? 20_000,
            TopCount = topCount ?? 10
        };
    }
}
