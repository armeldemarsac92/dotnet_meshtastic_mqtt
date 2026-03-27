using MeshBoard.Contracts.Collector;

namespace MeshBoard.Api.Public;

internal static class PublicCollectorRequestMappingExtensions
{
    public static CollectorMapQuery ToCollectorMapQuery(this CollectorMapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorMapQuery
        {
            ServerAddress = request.ServerAddress,
            Region = request.Region,
            ChannelName = request.ChannelName,
            ActiveWithinHours = request.ActiveWithinHours ?? 24,
            MaxNodes = request.MaxNodes ?? 5_000,
            MaxLinks = request.MaxLinks ?? 10_000
        };
    }

    public static CollectorPacketStatsQuery ToCollectorPacketStatsQuery(this CollectorPacketStatsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorPacketStatsQuery
        {
            ServerAddress = request.ServerAddress,
            Region = request.Region,
            ChannelName = request.ChannelName,
            PacketType = request.PacketType,
            LookbackHours = request.LookbackHours ?? 24 * 7,
            MaxRows = request.MaxRows ?? 500
        };
    }

    public static CollectorPacketStatsQuery ToCollectorPacketStatsQuery(this CollectorNodePacketStatsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorPacketStatsQuery
        {
            ServerAddress = request.ServerAddress,
            Region = request.Region,
            ChannelName = request.ChannelName,
            NodeId = request.NodeId,
            PacketType = request.PacketType,
            LookbackHours = request.LookbackHours ?? 24 * 7,
            MaxRows = request.MaxRows ?? 500
        };
    }

    public static CollectorNeighborLinkStatsQuery ToCollectorNeighborLinkStatsQuery(this CollectorNeighborLinkStatsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorNeighborLinkStatsQuery
        {
            ServerAddress = request.ServerAddress,
            Region = request.Region,
            ChannelName = request.ChannelName,
            SourceNodeId = request.SourceNodeId,
            TargetNodeId = request.TargetNodeId,
            LookbackHours = request.LookbackHours ?? 24 * 7,
            MaxRows = request.MaxRows ?? 500
        };
    }

    public static CollectorOverviewQuery ToCollectorOverviewQuery(this CollectorOverviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorOverviewQuery
        {
            ServerAddress = request.ServerAddress,
            Region = request.Region,
            ChannelName = request.ChannelName,
            ActiveWithinHours = request.ActiveWithinHours ?? 24,
            LookbackHours = request.LookbackHours ?? 24 * 7,
            MaxChannels = request.MaxChannels ?? 20,
            TopPacketTypes = request.TopPacketTypes ?? 3
        };
    }

    public static CollectorNodePageQuery ToCollectorNodePageQuery(this CollectorNodePageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorNodePageQuery
        {
            SearchText = request.SearchText,
            SortBy = request.SortBy ?? CollectorNodeSortOption.LastHeardDesc,
            Page = request.Page ?? 1,
            PageSize = request.PageSize ?? 50
        };
    }

    public static CollectorChannelPageQuery ToCollectorChannelPageQuery(this CollectorChannelPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorChannelPageQuery
        {
            SearchText = request.SearchText,
            SortBy = request.SortBy ?? CollectorChannelSortOption.LastObservedDesc,
            Page = request.Page ?? 1,
            PageSize = request.PageSize ?? 50
        };
    }

    public static CollectorTopologyQuery ToCollectorTopologyQuery(this CollectorTopologyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CollectorTopologyQuery
        {
            ServerAddress = request.ServerAddress,
            Region = request.Region,
            ChannelName = request.ChannelName,
            ActiveWithinHours = request.ActiveWithinHours ?? 24,
            MaxNodes = request.MaxNodes ?? 10_000,
            MaxLinks = request.MaxLinks ?? 20_000,
            TopCount = request.TopCount ?? 10
        };
    }
}
