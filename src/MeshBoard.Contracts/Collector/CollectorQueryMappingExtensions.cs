namespace MeshBoard.Contracts.Collector;

public static class CollectorQueryMappingExtensions
{
    public static CollectorMapQuery ToSanitizedCollectorMapQuery(
        this CollectorMapQuery? query,
        int defaultActiveWithinHours,
        int maxActiveWithinHours,
        int defaultMaxNodes,
        int maxNodes,
        int defaultMaxLinks,
        int maxLinks)
    {
        return new CollectorMapQuery
        {
            ServerAddress = Normalize(query?.ServerAddress),
            Region = Normalize(query?.Region),
            ChannelName = Normalize(query?.ChannelName),
            ActiveWithinHours = Clamp(query?.ActiveWithinHours ?? defaultActiveWithinHours, 1, maxActiveWithinHours),
            MaxNodes = Clamp(query?.MaxNodes ?? defaultMaxNodes, 1, maxNodes),
            MaxLinks = Clamp(query?.MaxLinks ?? defaultMaxLinks, 1, maxLinks)
        };
    }

    public static CollectorPacketStatsQuery ToSanitizedCollectorPacketStatsQuery(
        this CollectorPacketStatsQuery? query,
        int defaultLookbackHours,
        int maxLookbackHours,
        int defaultMaxRows,
        int maxRows)
    {
        return new CollectorPacketStatsQuery
        {
            ServerAddress = Normalize(query?.ServerAddress),
            Region = Normalize(query?.Region),
            ChannelName = Normalize(query?.ChannelName),
            NodeId = Normalize(query?.NodeId),
            PacketType = Normalize(query?.PacketType),
            LookbackHours = Clamp(query?.LookbackHours ?? defaultLookbackHours, 1, maxLookbackHours),
            MaxRows = Clamp(query?.MaxRows ?? defaultMaxRows, 1, maxRows)
        };
    }

    public static CollectorOverviewQuery ToSanitizedCollectorOverviewQuery(
        this CollectorOverviewQuery? query,
        int defaultActiveWithinHours,
        int maxActiveWithinHours,
        int defaultLookbackHours,
        int maxLookbackHours,
        int defaultMaxChannels,
        int maxChannels,
        int defaultTopPacketTypes,
        int maxTopPacketTypes)
    {
        return new CollectorOverviewQuery
        {
            ServerAddress = Normalize(query?.ServerAddress),
            Region = Normalize(query?.Region),
            ChannelName = Normalize(query?.ChannelName),
            ActiveWithinHours = Clamp(query?.ActiveWithinHours ?? defaultActiveWithinHours, 1, maxActiveWithinHours),
            LookbackHours = Clamp(query?.LookbackHours ?? defaultLookbackHours, 1, maxLookbackHours),
            MaxChannels = Clamp(query?.MaxChannels ?? defaultMaxChannels, 1, maxChannels),
            TopPacketTypes = Clamp(query?.TopPacketTypes ?? defaultTopPacketTypes, 1, maxTopPacketTypes)
        };
    }

    public static CollectorNeighborLinkStatsQuery ToSanitizedCollectorNeighborLinkStatsQuery(
        this CollectorNeighborLinkStatsQuery? query,
        int defaultLookbackHours,
        int maxLookbackHours,
        int defaultMaxRows,
        int maxRows)
    {
        return new CollectorNeighborLinkStatsQuery
        {
            ServerAddress = Normalize(query?.ServerAddress),
            Region = Normalize(query?.Region),
            ChannelName = Normalize(query?.ChannelName),
            SourceNodeId = Normalize(query?.SourceNodeId),
            TargetNodeId = Normalize(query?.TargetNodeId),
            LookbackHours = Clamp(query?.LookbackHours ?? defaultLookbackHours, 1, maxLookbackHours),
            MaxRows = Clamp(query?.MaxRows ?? defaultMaxRows, 1, maxRows)
        };
    }

    public static CollectorTopologyQuery ToSanitizedCollectorTopologyQuery(
        this CollectorTopologyQuery? query,
        int defaultActiveWithinHours,
        int maxActiveWithinHours,
        int defaultMaxNodes,
        int maxNodes,
        int defaultMaxLinks,
        int maxLinks,
        int defaultTopCount,
        int maxTopCount)
    {
        return new CollectorTopologyQuery
        {
            ServerAddress = Normalize(query?.ServerAddress),
            Region = Normalize(query?.Region),
            ChannelName = Normalize(query?.ChannelName),
            ActiveWithinHours = Clamp(query?.ActiveWithinHours ?? defaultActiveWithinHours, 1, maxActiveWithinHours),
            MaxNodes = Clamp(query?.MaxNodes ?? defaultMaxNodes, 1, maxNodes),
            MaxLinks = Clamp(query?.MaxLinks ?? defaultMaxLinks, 1, maxLinks),
            TopCount = Clamp(query?.TopCount ?? defaultTopCount, 1, maxTopCount)
        };
    }

    public static CollectorNodePageQuery ToSanitizedCollectorNodePageQuery(
        this CollectorNodePageQuery? query,
        int defaultPageSize,
        int maxPageSize)
    {
        return new CollectorNodePageQuery
        {
            SearchText = Normalize(query?.SearchText),
            SortBy = query?.SortBy ?? CollectorNodeSortOption.LastHeardDesc,
            Page = Math.Max(1, query?.Page ?? 1),
            PageSize = Clamp(query?.PageSize ?? defaultPageSize, 1, maxPageSize)
        };
    }

    public static CollectorChannelPageQuery ToSanitizedCollectorChannelPageQuery(
        this CollectorChannelPageQuery? query,
        int defaultPageSize,
        int maxPageSize)
    {
        return new CollectorChannelPageQuery
        {
            SearchText = Normalize(query?.SearchText),
            SortBy = query?.SortBy ?? CollectorChannelSortOption.LastObservedDesc,
            Page = Math.Max(1, query?.Page ?? 1),
            PageSize = Clamp(query?.PageSize ?? defaultPageSize, 1, maxPageSize)
        };
    }

    public static CollectorMapQuery ToCollectorMapQuery(
        this CollectorOverviewQuery query,
        int maxNodes,
        int maxLinks)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorMapQuery
        {
            ServerAddress = query.ServerAddress,
            Region = query.Region,
            ChannelName = query.ChannelName,
            ActiveWithinHours = query.ActiveWithinHours,
            MaxNodes = maxNodes,
            MaxLinks = maxLinks
        };
    }

    public static CollectorTopologyQuery ToCollectorTopologyQuery(
        this CollectorOverviewQuery query,
        string serverAddress,
        int topCount,
        int maxNodes,
        int maxLinks)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorTopologyQuery
        {
            ServerAddress = serverAddress,
            Region = query.Region,
            ChannelName = query.ChannelName,
            ActiveWithinHours = query.ActiveWithinHours,
            MaxNodes = maxNodes,
            MaxLinks = maxLinks,
            TopCount = topCount
        };
    }

    public static CollectorPacketStatsQuery ToCollectorPacketStatsQuery(
        this CollectorOverviewQuery query,
        string serverAddress,
        int maxRows)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorPacketStatsQuery
        {
            ServerAddress = serverAddress,
            Region = query.Region,
            ChannelName = query.ChannelName,
            LookbackHours = query.LookbackHours,
            MaxRows = maxRows
        };
    }

    public static CollectorNeighborLinkStatsQuery ToCollectorNeighborLinkStatsQuery(
        this CollectorOverviewQuery query,
        string serverAddress,
        int maxRows)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorNeighborLinkStatsQuery
        {
            ServerAddress = serverAddress,
            Region = query.Region,
            ChannelName = query.ChannelName,
            LookbackHours = query.LookbackHours,
            MaxRows = maxRows
        };
    }

    public static CollectorNeighborLinkStatsQuery ToCollectorNeighborLinkStatsQuery(
        this CollectorTopologyQuery query,
        int maxRows)
    {
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorNeighborLinkStatsQuery
        {
            ServerAddress = query.ServerAddress,
            Region = query.Region,
            ChannelName = query.ChannelName,
            LookbackHours = query.ActiveWithinHours,
            MaxRows = maxRows
        };
    }

    public static CollectorTopologyQuery ToCollectorTopologyQuery(
        this CollectorChannelSummary channel,
        CollectorOverviewQuery query,
        int topCount,
        int maxNodes,
        int maxLinks)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorTopologyQuery
        {
            ServerAddress = channel.ServerAddress,
            Region = channel.Region,
            ChannelName = channel.ChannelName,
            ActiveWithinHours = query.ActiveWithinHours,
            MaxNodes = maxNodes,
            MaxLinks = maxLinks,
            TopCount = topCount
        };
    }

    public static CollectorPacketStatsQuery ToCollectorPacketStatsQuery(
        this CollectorChannelSummary channel,
        CollectorOverviewQuery query,
        int maxRows)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorPacketStatsQuery
        {
            ServerAddress = channel.ServerAddress,
            Region = channel.Region,
            ChannelName = channel.ChannelName,
            LookbackHours = query.LookbackHours,
            MaxRows = maxRows
        };
    }

    public static CollectorNeighborLinkStatsQuery ToCollectorNeighborLinkStatsQuery(
        this CollectorChannelSummary channel,
        CollectorOverviewQuery query,
        int maxRows)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(query);

        return new CollectorNeighborLinkStatsQuery
        {
            ServerAddress = channel.ServerAddress,
            Region = channel.Region,
            ChannelName = channel.ChannelName,
            LookbackHours = query.LookbackHours,
            MaxRows = maxRows
        };
    }

    public static string? NullIfWhiteSpace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return Math.Min(value, max);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
