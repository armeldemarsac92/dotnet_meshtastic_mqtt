namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class NodeQueries
{
    public static string GetNodes =>
        """
        SELECT
            node_id AS NodeId,
            short_name AS ShortName,
            long_name AS LongName,
            last_heard_at_utc AS LastHeardAtUtc,
            last_text_message_at_utc AS LastTextMessageAtUtc,
            last_known_latitude AS LastKnownLatitude,
            last_known_longitude AS LastKnownLongitude
        FROM nodes
        ORDER BY COALESCE(long_name, short_name, node_id);
        """;

    public static string UpsertNode =>
        """
        INSERT INTO nodes (
            node_id,
            short_name,
            long_name,
            last_heard_at_utc,
            last_text_message_at_utc,
            last_known_latitude,
            last_known_longitude)
        VALUES (
            @NodeId,
            @ShortName,
            @LongName,
            @LastHeardAtUtc,
            @LastTextMessageAtUtc,
            @LastKnownLatitude,
            @LastKnownLongitude)
        ON CONFLICT(node_id) DO UPDATE SET
            short_name = COALESCE(excluded.short_name, nodes.short_name),
            long_name = COALESCE(excluded.long_name, nodes.long_name),
            last_heard_at_utc = COALESCE(excluded.last_heard_at_utc, nodes.last_heard_at_utc),
            last_text_message_at_utc = COALESCE(excluded.last_text_message_at_utc, nodes.last_text_message_at_utc),
            last_known_latitude = COALESCE(excluded.last_known_latitude, nodes.last_known_latitude),
            last_known_longitude = COALESCE(excluded.last_known_longitude, nodes.last_known_longitude);
        """;
}
