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
            packet_type TEXT NOT NULL,
            message_key TEXT NOT NULL,
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

    public static string GetMessageHistoryColumns =>
        """
        PRAGMA table_info(message_history);
        """;

    public static string AddMessageHistoryPacketTypeColumn =>
        """
        ALTER TABLE message_history
        ADD COLUMN packet_type TEXT NULL;
        """;

    public static string AddMessageHistoryMessageKeyColumn =>
        """
        ALTER TABLE message_history
        ADD COLUMN message_key TEXT NULL;
        """;

    public static string BackfillMessageHistoryPacketType =>
        """
        UPDATE message_history
        SET packet_type = CASE
            WHEN packet_type IS NOT NULL AND packet_type <> '' THEN packet_type
            WHEN payload_preview LIKE 'Node info:%' THEN 'Node Info'
            WHEN payload_preview LIKE 'Position:%' THEN 'Position Update'
            WHEN payload_preview LIKE 'Telemetry payload%' THEN 'Telemetry'
            WHEN payload_preview LIKE 'Compressed text payload%' THEN 'Compressed Text Message'
            WHEN payload_preview LIKE 'Non-decoded Meshtastic payload%' THEN 'Encrypted Packet'
            ELSE 'Legacy Packet'
        END
        WHERE packet_type IS NULL OR packet_type = '';
        """;

    public static string BackfillMessageHistoryMessageKey =>
        """
        UPDATE message_history
        SET message_key = id
        WHERE message_key IS NULL OR message_key = '';
        """;

    public static string CreateMessageHistoryMessageKeyIndex =>
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_message_history_message_key
            ON message_history(message_key);
        """;
}
