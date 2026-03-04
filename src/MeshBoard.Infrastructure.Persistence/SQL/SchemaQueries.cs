namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class SchemaQueries
{
    public static string CreateSchema =>
        """
        CREATE TABLE IF NOT EXISTS favorite_nodes (
            id TEXT NOT NULL PRIMARY KEY,
            node_id TEXT NOT NULL,
            short_name TEXT NULL,
            long_name TEXT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_favorite_nodes_node_id
            ON favorite_nodes(node_id);

        CREATE TABLE IF NOT EXISTS topic_presets (
            id TEXT NOT NULL PRIMARY KEY,
            name TEXT NOT NULL,
            topic_pattern TEXT NOT NULL,
            is_default INTEGER NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_topic_presets_topic_pattern
            ON topic_presets(topic_pattern);

        CREATE TABLE IF NOT EXISTS nodes (
            node_id TEXT NOT NULL PRIMARY KEY,
            short_name TEXT NULL,
            long_name TEXT NULL,
            last_heard_at_utc TEXT NULL,
            last_text_message_at_utc TEXT NULL,
            last_known_latitude REAL NULL,
            last_known_longitude REAL NULL
        );

        CREATE TABLE IF NOT EXISTS message_history (
            id TEXT NOT NULL PRIMARY KEY,
            topic TEXT NOT NULL,
            from_node_id TEXT NOT NULL,
            to_node_id TEXT NULL,
            payload_preview TEXT NOT NULL,
            is_private INTEGER NOT NULL,
            received_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_message_history_received_at_utc
            ON message_history(received_at_utc DESC);
        """;

    public static string DeleteExpiredMessages =>
        """
        DELETE FROM message_history
        WHERE received_at_utc < @CutoffUtc;
        """;
}
