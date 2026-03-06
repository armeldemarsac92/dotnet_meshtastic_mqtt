namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class SchemaQueries
{
    public static string EnableWriteAheadLogging =>
        """
        PRAGMA journal_mode = WAL;
        """;

    public static string SetSynchronousNormal =>
        """
        PRAGMA synchronous = NORMAL;
        """;

    public static string SetTempStoreMemory =>
        """
        PRAGMA temp_store = MEMORY;
        """;

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
            encryption_key_base64 TEXT NULL,
            is_default INTEGER NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_topic_presets_topic_pattern
            ON topic_presets(topic_pattern);

        CREATE TABLE IF NOT EXISTS broker_server_profiles (
            id TEXT NOT NULL PRIMARY KEY,
            name TEXT NOT NULL,
            host TEXT NOT NULL,
            port INTEGER NOT NULL,
            use_tls INTEGER NOT NULL,
            username TEXT NULL,
            password TEXT NULL,
            default_topic_pattern TEXT NOT NULL,
            default_encryption_key_base64 TEXT NOT NULL,
            downlink_topic TEXT NOT NULL,
            enable_send INTEGER NOT NULL,
            is_active INTEGER NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_broker_server_profiles_name
            ON broker_server_profiles(name);

        CREATE TABLE IF NOT EXISTS discovered_topics (
            topic_pattern TEXT NOT NULL PRIMARY KEY,
            region TEXT NOT NULL,
            channel TEXT NOT NULL,
            first_observed_at_utc TEXT NOT NULL,
            last_observed_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_discovered_topics_last_observed_at_utc
            ON discovered_topics(last_observed_at_utc DESC);

        CREATE TABLE IF NOT EXISTS nodes (
            node_id TEXT NOT NULL PRIMARY KEY,
            broker_server TEXT NULL,
            short_name TEXT NULL,
            long_name TEXT NULL,
            last_heard_at_utc TEXT NULL,
            last_heard_channel TEXT NULL,
            last_text_message_at_utc TEXT NULL,
            last_known_latitude REAL NULL,
            last_known_longitude REAL NULL,
            battery_level_percent INTEGER NULL,
            voltage REAL NULL,
            channel_utilization REAL NULL,
            air_util_tx REAL NULL,
            uptime_seconds INTEGER NULL,
            temperature_celsius REAL NULL,
            relative_humidity REAL NULL,
            barometric_pressure REAL NULL
        );

        CREATE INDEX IF NOT EXISTS ix_nodes_last_heard_at_utc
            ON nodes(last_heard_at_utc DESC);

        CREATE TABLE IF NOT EXISTS message_history (
            id TEXT NOT NULL PRIMARY KEY,
            topic TEXT NOT NULL,
            broker_server TEXT NULL,
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

        CREATE INDEX IF NOT EXISTS ix_message_history_from_node_id_received_at_utc
            ON message_history(from_node_id, received_at_utc DESC);

        CREATE INDEX IF NOT EXISTS ix_message_history_topic_received_at_utc
            ON message_history(topic, received_at_utc DESC);
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

    public static string AddMessageHistoryBrokerServerColumn =>
        """
        ALTER TABLE message_history
        ADD COLUMN broker_server TEXT NULL;
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

    public static string BackfillMessageHistoryBrokerServer =>
        """
        UPDATE message_history
        SET broker_server = COALESCE(NULLIF(broker_server, ''), 'mqtt.meshtastic.org:1883')
        WHERE broker_server IS NULL OR broker_server = '';
        """;

    public static string CreateMessageHistoryMessageKeyIndex =>
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_message_history_message_key
            ON message_history(message_key);
        """;

    public static string GetNodeColumns =>
        """
        PRAGMA table_info(nodes);
        """;

    public static string GetTopicPresetColumns =>
        """
        PRAGMA table_info(topic_presets);
        """;

    public static string AddTopicPresetsEncryptionKeyBase64Column =>
        """
        ALTER TABLE topic_presets
        ADD COLUMN encryption_key_base64 TEXT NULL;
        """;

    public static string AddNodesBatteryLevelPercentColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN battery_level_percent INTEGER NULL;
        """;

    public static string AddNodesVoltageColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN voltage REAL NULL;
        """;

    public static string AddNodesChannelUtilizationColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN channel_utilization REAL NULL;
        """;

    public static string AddNodesAirUtilTxColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN air_util_tx REAL NULL;
        """;

    public static string AddNodesUptimeSecondsColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN uptime_seconds INTEGER NULL;
        """;

    public static string AddNodesTemperatureCelsiusColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN temperature_celsius REAL NULL;
        """;

    public static string AddNodesRelativeHumidityColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN relative_humidity REAL NULL;
        """;

    public static string AddNodesBarometricPressureColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN barometric_pressure REAL NULL;
        """;

    public static string AddNodesBrokerServerColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN broker_server TEXT NULL;
        """;

    public static string BackfillNodesBrokerServer =>
        """
        UPDATE nodes
        SET broker_server = COALESCE(NULLIF(broker_server, ''), 'mqtt.meshtastic.org:1883')
        WHERE broker_server IS NULL OR broker_server = '';
        """;

    public static string AddNodesLastHeardChannelColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN last_heard_channel TEXT NULL;
        """;

    public static string CreateNodesLastHeardChannelIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_nodes_last_heard_channel
            ON nodes(last_heard_channel);
        """;
}
