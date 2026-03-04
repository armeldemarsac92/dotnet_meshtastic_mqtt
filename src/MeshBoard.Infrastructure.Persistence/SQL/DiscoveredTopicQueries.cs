namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class DiscoveredTopicQueries
{
    public static string GetDiscoveredTopics =>
        """
        SELECT
            topic_pattern AS TopicPattern,
            region AS Region,
            channel AS Channel
        FROM discovered_topics
        ORDER BY last_observed_at_utc DESC, region ASC, channel ASC;
        """;

    public static string UpsertDiscoveredTopic =>
        """
        INSERT INTO discovered_topics (
            topic_pattern,
            region,
            channel,
            first_observed_at_utc,
            last_observed_at_utc)
        VALUES (
            @TopicPattern,
            @Region,
            @Channel,
            @ObservedAtUtc,
            @ObservedAtUtc)
        ON CONFLICT(topic_pattern) DO UPDATE SET
            region = excluded.region,
            channel = excluded.channel,
            last_observed_at_utc = MAX(discovered_topics.last_observed_at_utc, excluded.last_observed_at_utc);
        """;
}
