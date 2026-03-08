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
        WHERE workspace_id = @WorkspaceId
          AND broker_server = @BrokerServer
        ORDER BY last_observed_at_utc DESC, region ASC, channel ASC;
        """;

    public static string UpsertDiscoveredTopic =>
        """
        INSERT INTO discovered_topics (
            workspace_id,
            broker_server,
            topic_pattern,
            region,
            channel,
            first_observed_at_utc,
            last_observed_at_utc)
        VALUES (
            @WorkspaceId,
            @BrokerServer,
            @TopicPattern,
            @Region,
            @Channel,
            @ObservedAtUtc,
            @ObservedAtUtc)
        ON CONFLICT(workspace_id, broker_server, topic_pattern) DO UPDATE SET
            region = excluded.region,
            channel = excluded.channel,
            last_observed_at_utc = MAX(discovered_topics.last_observed_at_utc, excluded.last_observed_at_utc);
        """;
}
