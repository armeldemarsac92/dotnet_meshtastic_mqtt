using MeshBoard.Contracts.Collector;
using Microsoft.AspNetCore.Mvc;

namespace MeshBoard.Api.Public;

internal sealed class CollectorMapRequest
{
    [FromQuery(Name = "serverAddress")]
    public string? ServerAddress { get; set; }

    [FromQuery]
    public string? Region { get; set; }

    [FromQuery]
    public string? ChannelName { get; set; }

    [FromQuery]
    public int? ActiveWithinHours { get; set; }

    [FromQuery]
    public int? MaxNodes { get; set; }

    [FromQuery]
    public int? MaxLinks { get; set; }
}

internal sealed class CollectorPacketStatsRequest
{
    [FromQuery(Name = "serverAddress")]
    public string? ServerAddress { get; set; }

    [FromQuery]
    public string? Region { get; set; }

    [FromQuery]
    public string? ChannelName { get; set; }

    [FromQuery]
    public string? PacketType { get; set; }

    [FromQuery]
    public int? LookbackHours { get; set; }

    [FromQuery]
    public int? MaxRows { get; set; }
}

internal sealed class CollectorNodePacketStatsRequest
{
    [FromQuery(Name = "serverAddress")]
    public string? ServerAddress { get; set; }

    [FromQuery]
    public string? Region { get; set; }

    [FromQuery]
    public string? ChannelName { get; set; }

    [FromQuery]
    public string? NodeId { get; set; }

    [FromQuery]
    public string? PacketType { get; set; }

    [FromQuery]
    public int? LookbackHours { get; set; }

    [FromQuery]
    public int? MaxRows { get; set; }
}

internal sealed class CollectorNeighborLinkStatsRequest
{
    [FromQuery(Name = "serverAddress")]
    public string? ServerAddress { get; set; }

    [FromQuery]
    public string? Region { get; set; }

    [FromQuery]
    public string? ChannelName { get; set; }

    [FromQuery]
    public string? SourceNodeId { get; set; }

    [FromQuery]
    public string? TargetNodeId { get; set; }

    [FromQuery]
    public int? LookbackHours { get; set; }

    [FromQuery]
    public int? MaxRows { get; set; }
}

internal sealed class CollectorOverviewRequest
{
    [FromQuery(Name = "serverAddress")]
    public string? ServerAddress { get; set; }

    [FromQuery]
    public string? Region { get; set; }

    [FromQuery]
    public string? ChannelName { get; set; }

    [FromQuery]
    public int? ActiveWithinHours { get; set; }

    [FromQuery]
    public int? LookbackHours { get; set; }

    [FromQuery]
    public int? MaxChannels { get; set; }

    [FromQuery]
    public int? TopPacketTypes { get; set; }
}

internal sealed class CollectorNodePageRequest
{
    [FromQuery]
    public string? SearchText { get; set; }

    [FromQuery]
    public CollectorNodeSortOption? SortBy { get; set; }

    [FromQuery]
    public int? Page { get; set; }

    [FromQuery]
    public int? PageSize { get; set; }
}

internal sealed class CollectorChannelPageRequest
{
    [FromQuery]
    public string? SearchText { get; set; }

    [FromQuery]
    public CollectorChannelSortOption? SortBy { get; set; }

    [FromQuery]
    public int? Page { get; set; }

    [FromQuery]
    public int? PageSize { get; set; }
}

internal sealed class CollectorTopologyRequest
{
    [FromQuery(Name = "serverAddress")]
    public string? ServerAddress { get; set; }

    [FromQuery]
    public string? Region { get; set; }

    [FromQuery]
    public string? ChannelName { get; set; }

    [FromQuery]
    public int? ActiveWithinHours { get; set; }

    [FromQuery]
    public int? MaxNodes { get; set; }

    [FromQuery]
    public int? MaxLinks { get; set; }

    [FromQuery]
    public int? TopCount { get; set; }
}
