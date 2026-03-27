using System.Globalization;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class CollectorTopicMapping
{
    public static IReadOnlyCollection<TopicCatalogEntry> MapToTopicCatalogEntries(
        this IEnumerable<DiscoveredTopicSqlResponse> responses)
    {
        return responses.Select(MapToTopicCatalogEntry).ToList();
    }

    public static ChannelSummary ToChannelSummary(this ChannelSummarySqlResponse? response)
    {
        if (response is null)
        {
            return new ChannelSummary();
        }

        return new ChannelSummary
        {
            PacketCount = response.PacketCount,
            UniqueSenderCount = response.UniqueSenderCount,
            DecodedPacketCount = response.DecodedPacketCount,
            LastSeenAtUtc = ParseNullableDateTimeOffset(response.LastSeenAtUtc),
            ObservedBrokerServers = ParseBrokerServers(response.BrokerServersCsv)
        };
    }

    public static IReadOnlyCollection<ChannelTopNode> MapToChannelTopNodes(
        this IEnumerable<ChannelTopNodeSqlResponse> responses)
    {
        return responses.Select(MapToChannelTopNode).ToArray();
    }

    public static TopicCatalogEntry MapToTopicCatalogEntry(this DiscoveredTopicSqlResponse response)
    {
        return new TopicCatalogEntry
        {
            Label = $"{response.Region} · {response.Channel}",
            TopicPattern = response.TopicPattern,
            Region = response.Region,
            Channel = response.Channel,
            IsRecommended = false
        };
    }

    public static ChannelTopNode MapToChannelTopNode(this ChannelTopNodeSqlResponse response)
    {
        return new ChannelTopNode
        {
            NodeId = response.NodeId,
            DisplayName = response.DisplayName,
            PacketCount = response.PacketCount
        };
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : null;
    }

    private static IReadOnlyCollection<string> ParseBrokerServers(string? brokerServersCsv)
    {
        if (string.IsNullOrWhiteSpace(brokerServersCsv))
        {
            return [];
        }

        return brokerServersCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(server => server, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
