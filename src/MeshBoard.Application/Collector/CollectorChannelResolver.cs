using MeshBoard.Application.Abstractions.Meshtastic;

namespace MeshBoard.Application.Collector;

public sealed class CollectorChannelResolver : ICollectorChannelResolver
{
    public string? ResolveChannelKey(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5)
        {
            return null;
        }

        var channel = segments[4];
        return string.IsNullOrWhiteSpace(channel) ? null : channel;
    }

    public string? ResolveTopicPattern(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var normalizedTopic = topic.Trim();

        if (normalizedTopic.EndsWith("/#", StringComparison.Ordinal))
        {
            return normalizedTopic;
        }

        var lastSeparatorIndex = normalizedTopic.LastIndexOf('/');

        if (lastSeparatorIndex < 0)
        {
            return normalizedTopic;
        }

        var lastSegment = normalizedTopic[(lastSeparatorIndex + 1)..];

        return lastSegment.StartsWith('!')
            ? normalizedTopic[..lastSeparatorIndex]
            : normalizedTopic;
    }
}
