using System.Globalization;
using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class CollectorReadMapping
{
    public static IReadOnlyCollection<CollectorServerSummary> MapToCollectorServerSummaries(
        this IEnumerable<CollectorServerSummarySqlResponse> responses)
    {
        return responses.Select(MapToCollectorServerSummary).ToArray();
    }

    public static IReadOnlyCollection<CollectorChannelSummary> MapToCollectorChannelSummaries(
        this IEnumerable<CollectorChannelSummarySqlResponse> responses)
    {
        return responses.Select(MapToCollectorChannelSummary).ToArray();
    }

    public static IReadOnlyCollection<CollectorMapLinkSummary> MapToCollectorMapLinkSummaries(
        this IEnumerable<CollectorMapLinkSqlResponse> responses)
    {
        return responses.Select(MapToCollectorMapLinkSummary).ToArray();
    }

    public static IReadOnlyCollection<CollectorChannelPacketHourlyRollup> MapToCollectorChannelPacketHourlyRollups(
        this IEnumerable<CollectorChannelPacketHourlyRollupSqlResponse> responses)
    {
        return responses.Select(MapToCollectorChannelPacketHourlyRollup).ToArray();
    }

    public static IReadOnlyCollection<CollectorNodePacketHourlyRollup> MapToCollectorNodePacketHourlyRollups(
        this IEnumerable<CollectorNodePacketHourlyRollupSqlResponse> responses)
    {
        return responses.Select(MapToCollectorNodePacketHourlyRollup).ToArray();
    }

    public static IReadOnlyCollection<CollectorNeighborLinkHourlyRollup> MapToCollectorNeighborLinkHourlyRollups(
        this IEnumerable<CollectorNeighborLinkHourlyRollupSqlResponse> responses)
    {
        return responses.Select(MapToCollectorNeighborLinkHourlyRollup).ToArray();
    }

    public static IReadOnlyCollection<CollectorOverviewPacketTypeSummary> MapToCollectorOverviewPacketTypeSummaries(
        this IEnumerable<CollectorPacketTypeCountSqlResponse> responses)
    {
        return responses.Select(MapToCollectorOverviewPacketTypeSummary).ToArray();
    }

    public static CollectorNodePage ToCollectorNodePage(
        this IReadOnlyCollection<CollectorNodePageSqlResponse> responses,
        int page,
        int pageSize)
    {
        return new CollectorNodePage
        {
            Items = responses.Select(MapToNodeSummary).ToArray(),
            TotalCount = responses.FirstOrDefault()?.TotalCount ?? 0,
            Page = Math.Max(1, page),
            PageSize = pageSize
        };
    }

    public static CollectorChannelPage ToCollectorChannelPage(
        this IReadOnlyCollection<CollectorChannelPageSqlResponse> responses,
        int page,
        int pageSize)
    {
        return new CollectorChannelPage
        {
            Items = responses.Select(MapToCollectorChannelSummary).ToArray(),
            TotalCount = responses.FirstOrDefault()?.TotalCount ?? 0,
            Page = Math.Max(1, page),
            PageSize = pageSize
        };
    }

    public static CollectorServerSummary MapToCollectorServerSummary(this CollectorServerSummarySqlResponse response)
    {
        return new CollectorServerSummary
        {
            ServerAddress = response.ServerAddress,
            FirstObservedAtUtc = ParseTimestamp(response.FirstObservedAtUtc),
            LastObservedAtUtc = ParseTimestamp(response.LastObservedAtUtc),
            ChannelCount = response.ChannelCount,
            NodeCount = response.NodeCount,
            MessageCount = response.MessageCount,
            NeighborLinkCount = response.NeighborLinkCount
        };
    }

    public static CollectorChannelSummary MapToCollectorChannelSummary(this CollectorChannelSummarySqlResponse response)
    {
        return new CollectorChannelSummary
        {
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            TopicPattern = response.TopicPattern,
            FirstObservedAtUtc = ParseTimestamp(response.FirstObservedAtUtc),
            LastObservedAtUtc = ParseTimestamp(response.LastObservedAtUtc),
            NodeCount = response.NodeCount,
            MessageCount = response.MessageCount,
            NeighborLinkCount = response.NeighborLinkCount
        };
    }

    public static CollectorMapLinkSummary MapToCollectorMapLinkSummary(this CollectorMapLinkSqlResponse response)
    {
        return new CollectorMapLinkSummary
        {
            SourceNodeId = response.SourceNodeId,
            TargetNodeId = response.TargetNodeId,
            SnrDb = response.SnrDb,
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName
        };
    }

    public static CollectorChannelPacketHourlyRollup MapToCollectorChannelPacketHourlyRollup(
        this CollectorChannelPacketHourlyRollupSqlResponse response)
    {
        return new CollectorChannelPacketHourlyRollup
        {
            BucketStartUtc = ParseTimestamp(response.BucketStartUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            PacketType = response.PacketType,
            PacketCount = response.PacketCount,
            ActiveNodeCount = response.ActiveNodeCount,
            FirstSeenAtUtc = ParseTimestamp(response.FirstSeenAtUtc),
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc)
        };
    }

    public static CollectorNodePacketHourlyRollup MapToCollectorNodePacketHourlyRollup(
        this CollectorNodePacketHourlyRollupSqlResponse response)
    {
        return new CollectorNodePacketHourlyRollup
        {
            BucketStartUtc = ParseTimestamp(response.BucketStartUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            NodeId = response.NodeId,
            ShortName = response.ShortName,
            LongName = response.LongName,
            PacketType = response.PacketType,
            PacketCount = response.PacketCount,
            FirstSeenAtUtc = ParseTimestamp(response.FirstSeenAtUtc),
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc)
        };
    }

    public static CollectorNeighborLinkHourlyRollup MapToCollectorNeighborLinkHourlyRollup(
        this CollectorNeighborLinkHourlyRollupSqlResponse response)
    {
        return new CollectorNeighborLinkHourlyRollup
        {
            BucketStartUtc = ParseTimestamp(response.BucketStartUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            SourceNodeId = response.SourceNodeId,
            TargetNodeId = response.TargetNodeId,
            SourceShortName = response.SourceShortName,
            SourceLongName = response.SourceLongName,
            TargetShortName = response.TargetShortName,
            TargetLongName = response.TargetLongName,
            ObservationCount = response.ObservationCount,
            AverageSnrDb = response.AverageSnrDb,
            MaxSnrDb = response.MaxSnrDb,
            LastSnrDb = response.LastSnrDb,
            FirstSeenAtUtc = ParseTimestamp(response.FirstSeenAtUtc),
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc)
        };
    }

    public static CollectorOverviewPacketTypeSummary MapToCollectorOverviewPacketTypeSummary(
        this CollectorPacketTypeCountSqlResponse response)
    {
        return new CollectorOverviewPacketTypeSummary
        {
            PacketType = response.PacketType,
            PacketCount = response.PacketCount
        };
    }

    public static NodeSummary MapToNodeSummary(this CollectorNodePageSqlResponse response)
    {
        return new NodeSummary
        {
            NodeId = response.NodeId,
            BrokerServer = response.BrokerServer,
            ShortName = response.ShortName,
            LongName = response.LongName,
            LastHeardAtUtc = ParseNullableTimestamp(response.LastHeardAtUtc),
            LastHeardChannel = response.LastHeardChannel,
            LastTextMessageAtUtc = ParseNullableTimestamp(response.LastTextMessageAtUtc),
            LastKnownLatitude = response.LastKnownLatitude,
            LastKnownLongitude = response.LastKnownLongitude,
            BatteryLevelPercent = response.BatteryLevelPercent,
            Voltage = response.Voltage,
            ChannelUtilization = response.ChannelUtilization,
            AirUtilTx = response.AirUtilTx,
            UptimeSeconds = response.UptimeSeconds,
            TemperatureCelsius = response.TemperatureCelsius,
            RelativeHumidity = response.RelativeHumidity,
            BarometricPressure = response.BarometricPressure
        };
    }

    public static CollectorChannelSummary MapToCollectorChannelSummary(this CollectorChannelPageSqlResponse response)
    {
        return new CollectorChannelSummary
        {
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            TopicPattern = response.TopicPattern,
            FirstObservedAtUtc = ParseTimestamp(response.FirstObservedAtUtc),
            LastObservedAtUtc = ParseTimestamp(response.LastObservedAtUtc),
            NodeCount = response.NodeCount,
            MessageCount = response.MessageCount,
            NeighborLinkCount = response.NeighborLinkCount
        };
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset? ParseNullableTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : null;
    }
}
