namespace MeshBoard.Application.Realtime;

public interface IRealtimeTopicFilterAuthorizationService
{
    bool IsPublishAllowed(string topic, IReadOnlyCollection<string> allowedTopicPatterns);

    bool IsSubscriptionAllowed(string topicFilter, IReadOnlyCollection<string> allowedTopicPatterns);
}

public sealed class RealtimeTopicFilterAuthorizationService : IRealtimeTopicFilterAuthorizationService
{
    public bool IsPublishAllowed(string topic, IReadOnlyCollection<string> allowedTopicPatterns)
    {
        return IsTopicAuthorized(topic, allowedTopicPatterns);
    }

    public bool IsSubscriptionAllowed(string topicFilter, IReadOnlyCollection<string> allowedTopicPatterns)
    {
        return IsTopicAuthorized(NormalizeSharedSubscriptionFilter(topicFilter), allowedTopicPatterns);
    }

    private static bool IsTopicAuthorized(string topicFilter, IReadOnlyCollection<string> allowedTopicPatterns)
    {
        if (string.IsNullOrWhiteSpace(topicFilter) || allowedTopicPatterns.Count == 0)
        {
            return false;
        }

        var normalizedRequestedTopic = topicFilter.Trim();

        foreach (var allowedTopicPattern in allowedTopicPatterns)
        {
            if (string.IsNullOrWhiteSpace(allowedTopicPattern))
            {
                continue;
            }

            var normalizedAllowedTopic = allowedTopicPattern.Trim();
            if (string.Equals(normalizedRequestedTopic, normalizedAllowedTopic, StringComparison.Ordinal))
            {
                return true;
            }

            if (!normalizedAllowedTopic.EndsWith("/#", StringComparison.Ordinal)
                || normalizedAllowedTopic.Contains('+', StringComparison.Ordinal))
            {
                continue;
            }

            var allowedPrefix = normalizedAllowedTopic[..^2];
            if (string.Equals(normalizedRequestedTopic, allowedPrefix, StringComparison.Ordinal)
                || normalizedRequestedTopic.StartsWith($"{allowedPrefix}/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeSharedSubscriptionFilter(string topicFilter)
    {
        if (string.IsNullOrWhiteSpace(topicFilter) || !topicFilter.StartsWith("$share/", StringComparison.Ordinal))
        {
            return topicFilter;
        }

        var secondSeparatorIndex = topicFilter.IndexOf('/', "$share/".Length);
        if (secondSeparatorIndex < 0 || secondSeparatorIndex == topicFilter.Length - 1)
        {
            return topicFilter;
        }

        return topicFilter[(secondSeparatorIndex + 1)..];
    }
}
