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
            workspace_id TEXT NOT NULL,
            node_id TEXT NOT NULL,
            short_name TEXT NULL,
            long_name TEXT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_favorite_nodes_workspace_node_id
            ON favorite_nodes(workspace_id, node_id);

        CREATE TABLE IF NOT EXISTS topic_presets (
            id TEXT NOT NULL PRIMARY KEY,
            workspace_id TEXT NOT NULL,
            broker_server TEXT NOT NULL,
            name TEXT NOT NULL,
            topic_pattern TEXT NOT NULL,
            encryption_key_base64 TEXT NULL,
            is_default INTEGER NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS broker_server_profiles (
            id TEXT NOT NULL PRIMARY KEY,
            workspace_id TEXT NOT NULL,
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
            subscription_intents_initialized INTEGER NOT NULL DEFAULT 0,
            is_active INTEGER NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_broker_server_profiles_workspace_name
            ON broker_server_profiles(workspace_id, name);

        CREATE UNIQUE INDEX IF NOT EXISTS ux_topic_presets_workspace_broker_server_topic_pattern
            ON topic_presets(workspace_id, broker_server, topic_pattern);

        CREATE TABLE IF NOT EXISTS users (
            id TEXT NOT NULL PRIMARY KEY,
            username TEXT NOT NULL,
            normalized_username TEXT NOT NULL,
            password_hash TEXT NOT NULL,
            created_at_utc TEXT NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS ux_users_normalized_username
            ON users(normalized_username);

        CREATE TABLE IF NOT EXISTS subscription_intents (
            workspace_id TEXT NOT NULL,
            broker_server_profile_id TEXT NOT NULL,
            topic_filter TEXT NOT NULL,
            created_at_utc TEXT NOT NULL,
            PRIMARY KEY (workspace_id, broker_server_profile_id, topic_filter)
        );

        CREATE TABLE IF NOT EXISTS workspace_runtime_status (
            workspace_id TEXT NOT NULL PRIMARY KEY,
            active_server_profile_id TEXT NULL,
            active_server_name TEXT NULL,
            active_server_address TEXT NULL,
            is_connected INTEGER NOT NULL,
            last_status_message TEXT NULL,
            topic_filters_json TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS broker_runtime_commands (
            id TEXT NOT NULL PRIMARY KEY,
            workspace_id TEXT NOT NULL,
            command_type TEXT NOT NULL,
            topic TEXT NULL,
            payload TEXT NULL,
            topic_filter TEXT NULL,
            status TEXT NOT NULL,
            attempt_count INTEGER NOT NULL,
            created_at_utc TEXT NOT NULL,
            available_at_utc TEXT NOT NULL,
            leased_by TEXT NULL,
            leased_at_utc TEXT NULL,
            lease_expires_at_utc TEXT NULL,
            completed_at_utc TEXT NULL,
            failed_at_utc TEXT NULL,
            last_error TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_broker_runtime_commands_status_available_at_utc
            ON broker_runtime_commands(status, available_at_utc, created_at_utc);

        CREATE INDEX IF NOT EXISTS ix_broker_runtime_commands_workspace_status_created_at_utc
            ON broker_runtime_commands(workspace_id, status, created_at_utc);

        CREATE TABLE IF NOT EXISTS runtime_pipeline_status (
            workspace_id TEXT NOT NULL PRIMARY KEY,
            inbound_queue_capacity INTEGER NOT NULL,
            inbound_worker_count INTEGER NOT NULL,
            inbound_queue_depth INTEGER NOT NULL,
            inbound_oldest_message_age_ms INTEGER NOT NULL,
            inbound_enqueued_count INTEGER NOT NULL,
            inbound_dequeued_count INTEGER NOT NULL,
            inbound_dropped_count INTEGER NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS discovered_topics (
            workspace_id TEXT NOT NULL,
            broker_server TEXT NOT NULL,
            topic_pattern TEXT NOT NULL,
            region TEXT NOT NULL,
            channel TEXT NOT NULL,
            first_observed_at_utc TEXT NOT NULL,
            last_observed_at_utc TEXT NOT NULL,
            PRIMARY KEY (workspace_id, broker_server, topic_pattern)
        );

        CREATE INDEX IF NOT EXISTS ix_discovered_topics_last_observed_at_utc
            ON discovered_topics(workspace_id, broker_server, last_observed_at_utc DESC);

        CREATE TABLE IF NOT EXISTS nodes (
            workspace_id TEXT NOT NULL,
            node_id TEXT NOT NULL,
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
            barometric_pressure REAL NULL,
            PRIMARY KEY (workspace_id, node_id)
        );

        CREATE TABLE IF NOT EXISTS message_history (
            id TEXT NOT NULL PRIMARY KEY,
            workspace_id TEXT NOT NULL,
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

    public static string GetRuntimePipelineStatusColumns =>
        """
        PRAGMA table_info(runtime_pipeline_status);
        """;

    public static string DropRuntimePipelineStatusLegacyTable =>
        """
        DROP TABLE IF EXISTS runtime_pipeline_status_legacy;
        """;

    public static string RenameRuntimePipelineStatusToLegacy =>
        """
        ALTER TABLE runtime_pipeline_status RENAME TO runtime_pipeline_status_legacy;
        """;

    public static string RecreateRuntimePipelineStatusWithWorkspace =>
        """
        CREATE TABLE IF NOT EXISTS runtime_pipeline_status (
            workspace_id TEXT NOT NULL PRIMARY KEY,
            inbound_queue_capacity INTEGER NOT NULL,
            inbound_worker_count INTEGER NOT NULL,
            inbound_queue_depth INTEGER NOT NULL,
            inbound_oldest_message_age_ms INTEGER NOT NULL,
            inbound_enqueued_count INTEGER NOT NULL,
            inbound_dequeued_count INTEGER NOT NULL,
            inbound_dropped_count INTEGER NOT NULL,
            updated_at_utc TEXT NOT NULL
        );
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

    public static string AddMessageHistoryWorkspaceIdColumn =>
        """
        ALTER TABLE message_history
        ADD COLUMN workspace_id TEXT NULL;
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

    public static string BackfillMessageHistoryWorkspaceId =>
        """
        UPDATE message_history
        SET workspace_id = @WorkspaceId
        WHERE workspace_id IS NULL OR workspace_id = '';
        """;

    public static string DropMessageHistoryLegacyMessageKeyIndex =>
        """
        DROP INDEX IF EXISTS ux_message_history_message_key;
        """;

    public static string DropMessageHistoryLegacyReceivedAtIndex =>
        """
        DROP INDEX IF EXISTS ix_message_history_received_at_utc;
        """;

    public static string DropMessageHistoryLegacyFromNodeIndex =>
        """
        DROP INDEX IF EXISTS ix_message_history_from_node_id_received_at_utc;
        """;

    public static string DropMessageHistoryLegacyTopicIndex =>
        """
        DROP INDEX IF EXISTS ix_message_history_topic_received_at_utc;
        """;

    public static string DropMessageHistoryLegacyBrokerServerReceivedAtIndex =>
        """
        DROP INDEX IF EXISTS ix_message_history_broker_server_received_at_utc;
        """;

    public static string CreateMessageHistoryWorkspaceMessageKeyIndex =>
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_message_history_workspace_message_key
            ON message_history(workspace_id, message_key);
        """;

    public static string CreateMessageHistoryWorkspaceReceivedAtIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_message_history_workspace_received_at_utc
            ON message_history(workspace_id, received_at_utc DESC);
        """;

    public static string CreateMessageHistoryWorkspaceFromNodeIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_message_history_workspace_from_node_id_received_at_utc
            ON message_history(workspace_id, from_node_id, received_at_utc DESC);
        """;

    public static string CreateMessageHistoryWorkspaceTopicIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_message_history_workspace_topic_received_at_utc
            ON message_history(workspace_id, topic, received_at_utc DESC);
        """;

    public static string CreateMessageHistoryWorkspaceBrokerServerReceivedAtIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_message_history_workspace_broker_server_received_at_utc
            ON message_history(workspace_id, broker_server, received_at_utc DESC);
        """;

    public static string GetNodeColumns =>
        """
        PRAGMA table_info(nodes);
        """;

    public static string GetFavoriteNodeColumns =>
        """
        PRAGMA table_info(favorite_nodes);
        """;

    public static string AddFavoriteNodesWorkspaceIdColumn =>
        """
        ALTER TABLE favorite_nodes
        ADD COLUMN workspace_id TEXT NULL;
        """;

    public static string BackfillFavoriteNodesWorkspaceId =>
        """
        UPDATE favorite_nodes
        SET workspace_id = @WorkspaceId
        WHERE workspace_id IS NULL OR workspace_id = '';
        """;

    public static string DropFavoriteNodesLegacyNodeIdIndex =>
        """
        DROP INDEX IF EXISTS ux_favorite_nodes_node_id;
        """;

    public static string CreateFavoriteNodesWorkspaceNodeIdIndex =>
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_favorite_nodes_workspace_node_id
            ON favorite_nodes(workspace_id, node_id);
        """;

    public static string GetBrokerServerProfileColumns =>
        """
        PRAGMA table_info(broker_server_profiles);
        """;

    public static string AddBrokerServerProfilesWorkspaceIdColumn =>
        """
        ALTER TABLE broker_server_profiles
        ADD COLUMN workspace_id TEXT NULL;
        """;

    public static string BackfillBrokerServerProfilesWorkspaceId =>
        """
        UPDATE broker_server_profiles
        SET workspace_id = @WorkspaceId
        WHERE workspace_id IS NULL OR workspace_id = '';
        """;

    public static string AddBrokerServerProfilesSubscriptionIntentsInitializedColumn =>
        """
        ALTER TABLE broker_server_profiles
        ADD COLUMN subscription_intents_initialized INTEGER NULL;
        """;

    public static string BackfillBrokerServerProfilesSubscriptionIntentsInitialized =>
        """
        UPDATE broker_server_profiles
        SET subscription_intents_initialized = COALESCE(subscription_intents_initialized, 0)
        WHERE subscription_intents_initialized IS NULL;
        """;

    public static string DropBrokerServerProfilesLegacyNameIndex =>
        """
        DROP INDEX IF EXISTS ux_broker_server_profiles_name;
        """;

    public static string CreateBrokerServerProfilesWorkspaceNameIndex =>
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_broker_server_profiles_workspace_name
            ON broker_server_profiles(workspace_id, name);
        """;

    public static string GetDiscoveredTopicColumns =>
        """
        PRAGMA table_info(discovered_topics);
        """;

    public static string DropDiscoveredTopicsLegacyTable =>
        """
        DROP TABLE IF EXISTS discovered_topics_legacy;
        """;

    public static string RenameDiscoveredTopicsToLegacy =>
        """
        ALTER TABLE discovered_topics RENAME TO discovered_topics_legacy;
        """;

    public static string RecreateDiscoveredTopicsWithBrokerServer =>
        """
        CREATE TABLE IF NOT EXISTS discovered_topics (
            workspace_id TEXT NOT NULL,
            broker_server TEXT NOT NULL,
            topic_pattern TEXT NOT NULL,
            region TEXT NOT NULL,
            channel TEXT NOT NULL,
            first_observed_at_utc TEXT NOT NULL,
            last_observed_at_utc TEXT NOT NULL,
            PRIMARY KEY (workspace_id, broker_server, topic_pattern)
        );
        """;

    public static string CreateDiscoveredTopicsLastObservedIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_discovered_topics_last_observed_at_utc
            ON discovered_topics(workspace_id, broker_server, last_observed_at_utc DESC);
        """;

    public static string CopyDiscoveredTopicsFromLegacyWithoutBrokerServer =>
        """
        INSERT INTO discovered_topics (
            workspace_id,
            broker_server,
            topic_pattern,
            region,
            channel,
            first_observed_at_utc,
            last_observed_at_utc)
        SELECT
            @WorkspaceId AS workspace_id,
            @BrokerServer AS broker_server,
            topic_pattern,
            region,
            channel,
            first_observed_at_utc,
            last_observed_at_utc
        FROM discovered_topics_legacy;
        """;

    public static string CopyDiscoveredTopicsFromLegacyWithBrokerServer =>
        """
        INSERT INTO discovered_topics (
            workspace_id,
            broker_server,
            topic_pattern,
            region,
            channel,
            first_observed_at_utc,
            last_observed_at_utc)
        SELECT
            @WorkspaceId AS workspace_id,
            COALESCE(NULLIF(broker_server, ''), @BrokerServer) AS broker_server,
            topic_pattern,
            region,
            channel,
            first_observed_at_utc,
            last_observed_at_utc
        FROM discovered_topics_legacy;
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

    public static string AddTopicPresetsBrokerServerColumn =>
        """
        ALTER TABLE topic_presets
        ADD COLUMN broker_server TEXT NULL;
        """;

    public static string AddTopicPresetsWorkspaceIdColumn =>
        """
        ALTER TABLE topic_presets
        ADD COLUMN workspace_id TEXT NULL;
        """;

    public static string BackfillTopicPresetsBrokerServer =>
        """
        UPDATE topic_presets
        SET broker_server = COALESCE(NULLIF(broker_server, ''), @BrokerServer)
        WHERE broker_server IS NULL OR broker_server = '';
        """;

    public static string BackfillTopicPresetsWorkspaceId =>
        """
        UPDATE topic_presets
        SET workspace_id = @WorkspaceId
        WHERE workspace_id IS NULL OR workspace_id = '';
        """;

    public static string DropTopicPresetsLegacyTopicPatternIndex =>
        """
        DROP INDEX IF EXISTS ux_topic_presets_topic_pattern;
        """;

    public static string DropTopicPresetsLegacyBrokerServerTopicPatternIndex =>
        """
        DROP INDEX IF EXISTS ux_topic_presets_broker_server_topic_pattern;
        """;

    public static string CreateTopicPresetsWorkspaceBrokerServerTopicPatternIndex =>
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_topic_presets_workspace_broker_server_topic_pattern
            ON topic_presets(workspace_id, broker_server, topic_pattern);
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

    public static string AddNodesWorkspaceIdColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN workspace_id TEXT NULL;
        """;

    public static string BackfillNodesBrokerServer =>
        """
        UPDATE nodes
        SET broker_server = COALESCE(NULLIF(broker_server, ''), 'mqtt.meshtastic.org:1883')
        WHERE broker_server IS NULL OR broker_server = '';
        """;

    public static string BackfillNodesWorkspaceId =>
        """
        UPDATE nodes
        SET workspace_id = @WorkspaceId
        WHERE workspace_id IS NULL OR workspace_id = '';
        """;

    public static string AddNodesLastHeardChannelColumn =>
        """
        ALTER TABLE nodes
        ADD COLUMN last_heard_channel TEXT NULL;
        """;

    public static string DropNodesLegacyTable =>
        """
        DROP TABLE IF EXISTS nodes_legacy;
        """;

    public static string RenameNodesToLegacy =>
        """
        ALTER TABLE nodes RENAME TO nodes_legacy;
        """;

    public static string RecreateNodesWithWorkspace =>
        """
        CREATE TABLE IF NOT EXISTS nodes (
            workspace_id TEXT NOT NULL,
            node_id TEXT NOT NULL,
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
            barometric_pressure REAL NULL,
            PRIMARY KEY (workspace_id, node_id)
        );
        """;

    public static string CopyNodesFromLegacy =>
        """
        INSERT INTO nodes (
            workspace_id,
            node_id,
            broker_server,
            short_name,
            long_name,
            last_heard_at_utc,
            last_heard_channel,
            last_text_message_at_utc,
            last_known_latitude,
            last_known_longitude,
            battery_level_percent,
            voltage,
            channel_utilization,
            air_util_tx,
            uptime_seconds,
            temperature_celsius,
            relative_humidity,
            barometric_pressure)
        SELECT
            workspace_id,
            node_id,
            broker_server,
            short_name,
            long_name,
            last_heard_at_utc,
            last_heard_channel,
            last_text_message_at_utc,
            last_known_latitude,
            last_known_longitude,
            battery_level_percent,
            voltage,
            channel_utilization,
            air_util_tx,
            uptime_seconds,
            temperature_celsius,
            relative_humidity,
            barometric_pressure
        FROM nodes_legacy;
        """;

    public static string CreateNodesWorkspaceLastHeardAtIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_nodes_workspace_last_heard_at_utc
            ON nodes(workspace_id, last_heard_at_utc DESC);
        """;

    public static string CreateNodesWorkspaceLastHeardChannelIndex =>
        """
        CREATE INDEX IF NOT EXISTS ix_nodes_workspace_last_heard_channel
            ON nodes(workspace_id, last_heard_channel);
        """;
}
