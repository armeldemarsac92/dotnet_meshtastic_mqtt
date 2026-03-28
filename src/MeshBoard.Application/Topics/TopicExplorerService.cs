using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Topics;

public interface ITopicExplorerService
{
    string CreatePresetName(TopicCatalogEntry entry);

    IReadOnlyCollection<TopicCatalogEntry> GetDiscoveredTopics(IEnumerable<string> topicValues);

    IReadOnlyCollection<TopicCatalogEntry> GetRecommendedTopics();
}

public sealed class TopicExplorerService : ITopicExplorerService
{
    private static readonly string[] RecommendedRegions =
    [
        "US",
        "EU_433",
        "EU_868",
        "ANZ"
    ];

    private static readonly string[] RecommendedChannels =
    [
        "LongFast",
        "MediumFast",
        "LongSlow",
        "MediumSlow",
        "ShortFast"
    ];

    public string CreatePresetName(TopicCatalogEntry entry)
    {
        return entry.IsRecommended
            ? entry.Label
            : $"Observed {entry.Region} {entry.Channel}";
    }

    public IReadOnlyCollection<TopicCatalogEntry> GetDiscoveredTopics(IEnumerable<string> topicValues)
    {
        var discoveredEntries = topicValues
            .Select(TryMapTopic)
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .GroupBy(entry => entry.TopicPattern, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.Region, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Channel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return discoveredEntries;
    }

    public IReadOnlyCollection<TopicCatalogEntry> GetRecommendedTopics()
    {
        return RecommendedRegions
            .SelectMany(
                region =>
                    RecommendedChannels.Select(
                        channel => (region, channel, $"msh/{region}/2/e/{channel}/#", true).ToTopicCatalogEntry()))
            .ToList();
    }

    private static TopicCatalogEntry? TryMapTopic(string topicValue)
    {
        if (string.IsNullOrWhiteSpace(topicValue))
        {
            return null;
        }

        var segments = topicValue
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5)
        {
            return null;
        }

        if (!string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var transport = segments[3];

        if (!string.Equals(transport, "e", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(transport, "json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var region = segments[1];
        var version = segments[2];
        var channel = segments[4];

        if (string.IsNullOrWhiteSpace(region) ||
            string.IsNullOrWhiteSpace(version) ||
            string.IsNullOrWhiteSpace(channel) ||
            channel is "#" or "+")
        {
            return null;
        }

        return (region, channel, $"msh/{region}/{version}/e/{channel}/#", false).ToTopicCatalogEntry();
    }
}
