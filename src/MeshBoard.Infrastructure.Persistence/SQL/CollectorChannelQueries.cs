namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class CollectorChannelQueries
{
    public static string UpsertServer =>
        """
        INSERT INTO collector_servers (
            server_address,
            first_observed_at_utc,
            last_observed_at_utc)
        VALUES (
            @ServerAddress,
            @ObservedAtUtc,
            @ObservedAtUtc)
        ON CONFLICT(server_address) DO UPDATE SET
            last_observed_at_utc = GREATEST(collector_servers.last_observed_at_utc, EXCLUDED.last_observed_at_utc)
        RETURNING id;
        """;

    public static string UpsertChannel =>
        """
        INSERT INTO collector_channels (
            server_id,
            region,
            mesh_version,
            channel_name,
            topic_pattern,
            first_observed_at_utc,
            last_observed_at_utc)
        VALUES (
            @ServerId,
            @Region,
            @MeshVersion,
            @ChannelName,
            @TopicPattern,
            @ObservedAtUtc,
            @ObservedAtUtc)
        ON CONFLICT(server_id, region, mesh_version, channel_name) DO UPDATE SET
            topic_pattern = EXCLUDED.topic_pattern,
            last_observed_at_utc = GREATEST(collector_channels.last_observed_at_utc, EXCLUDED.last_observed_at_utc)
        RETURNING id;
        """;

    public static string GetDiscoveredTopics =>
        """
        SELECT
            c.topic_pattern AS TopicPattern,
            c.region AS Region,
            c.channel_name AS Channel
        FROM collector_channels c
        INNER JOIN collector_servers s
            ON s.id = c.server_id
        WHERE s.server_address = @BrokerServer
        ORDER BY c.last_observed_at_utc DESC,
                 c.region ASC,
                 c.channel_name ASC;
        """;
}
