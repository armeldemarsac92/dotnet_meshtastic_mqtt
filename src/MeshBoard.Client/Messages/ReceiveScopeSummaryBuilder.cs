using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.Client.Messages;

public sealed class ReceiveScopeSummaryBuilder
{
    public ReceiveScopeSummary Build(
        SavedBrokerServerProfile? activeServer,
        IReadOnlyCollection<SavedTopicPreset> presets,
        IReadOnlyCollection<SavedChannelFilter> channels)
    {
        if (activeServer is null)
        {
            return ReceiveScopeSummary.Empty;
        }

        var topics = new List<ReceiveScopeTopic>();
        var indexedTopics = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var preset in presets
                     .Where(item => item.ServerProfileId == activeServer.Id)
                     .OrderByDescending(item => item.IsDefault)
                     .ThenBy(item => item.TopicPattern, StringComparer.Ordinal))
        {
            AddOrMergeTopic(topics, indexedTopics, preset.TopicPattern, "Preset");
        }

        foreach (var channel in channels
                     .Where(item => item.BrokerServerProfileId == activeServer.Id)
                     .OrderBy(item => item.TopicFilter, StringComparer.Ordinal))
        {
            AddOrMergeTopic(topics, indexedTopics, channel.TopicFilter, "Channel");
        }

        var usesFallbackTopic = false;
        if (topics.Count == 0 && !string.IsNullOrWhiteSpace(activeServer.DefaultTopicPattern))
        {
            usesFallbackTopic = true;
            topics.Add(
                new ReceiveScopeTopic
                {
                    TopicPattern = activeServer.DefaultTopicPattern.Trim(),
                    SourceLabel = "Fallback",
                    IsFallback = true
                });
        }

        return new ReceiveScopeSummary
        {
            HasActiveServer = true,
            ServerName = activeServer.Name,
            ServerAddress = activeServer.ServerAddress,
            UsesFallbackTopic = usesFallbackTopic,
            Topics = topics
        };
    }

    private static void AddOrMergeTopic(
        List<ReceiveScopeTopic> topics,
        Dictionary<string, int> indexedTopics,
        string topicPattern,
        string sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(topicPattern))
        {
            return;
        }

        var normalizedTopic = topicPattern.Trim();

        if (indexedTopics.TryGetValue(normalizedTopic, out var index))
        {
            var existingTopic = topics[index];
            if (!string.Equals(existingTopic.SourceLabel, sourceLabel, StringComparison.Ordinal)
                && !existingTopic.SourceLabel.Contains(sourceLabel, StringComparison.Ordinal))
            {
                topics[index] = new ReceiveScopeTopic
                {
                    TopicPattern = existingTopic.TopicPattern,
                    SourceLabel = $"{existingTopic.SourceLabel} + {sourceLabel}",
                    IsFallback = existingTopic.IsFallback
                };
            }

            return;
        }

        indexedTopics[normalizedTopic] = topics.Count;
        topics.Add(
            new ReceiveScopeTopic
            {
                TopicPattern = normalizedTopic,
                SourceLabel = sourceLabel
            });
    }
}
