namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class TopicPresetQueries
{
    public static string ClearDefaultTopicPresets =>
        """
        UPDATE topic_presets
        SET is_default = 0
        WHERE is_default = 1
          AND workspace_id = @WorkspaceId
          AND broker_server = @BrokerServer;
        """;

    public static string GetTopicPresetByTopicPattern =>
        """
        SELECT
            id AS Id,
            name AS Name,
            topic_pattern AS TopicPattern,
            encryption_key_base64 AS EncryptionKeyBase64,
            is_default AS IsDefault,
            created_at_utc AS CreatedAtUtc
        FROM topic_presets
        WHERE workspace_id = @WorkspaceId
          AND broker_server = @BrokerServer
          AND topic_pattern = @TopicPattern;
        """;

    public static string GetTopicPresets =>
        """
        SELECT
            id AS Id,
            name AS Name,
            topic_pattern AS TopicPattern,
            encryption_key_base64 AS EncryptionKeyBase64,
            is_default AS IsDefault,
            created_at_utc AS CreatedAtUtc
        FROM topic_presets
        WHERE workspace_id = @WorkspaceId
          AND broker_server = @BrokerServer
        ORDER BY is_default DESC, name ASC;
        """;

    public static string InsertTopicPresetIfMissing =>
        """
        INSERT INTO topic_presets (
            id,
            workspace_id,
            broker_server,
            name,
            topic_pattern,
            encryption_key_base64,
            is_default,
            created_at_utc)
        VALUES (
            @Id,
            @WorkspaceId,
            @BrokerServer,
            @Name,
            @TopicPattern,
            @EncryptionKeyBase64,
            @IsDefault,
            @CreatedAtUtc)
        ON CONFLICT(workspace_id, broker_server, topic_pattern) DO NOTHING;
        """;

    public static string UpsertTopicPreset =>
        """
        INSERT INTO topic_presets (
            id,
            workspace_id,
            broker_server,
            name,
            topic_pattern,
            encryption_key_base64,
            is_default,
            created_at_utc)
        VALUES (
            @Id,
            @WorkspaceId,
            @BrokerServer,
            @Name,
            @TopicPattern,
            @EncryptionKeyBase64,
            @IsDefault,
            @CreatedAtUtc)
        ON CONFLICT(workspace_id, broker_server, topic_pattern) DO UPDATE SET
            name = excluded.name,
            encryption_key_base64 = excluded.encryption_key_base64,
            is_default = excluded.is_default;
        """;
}
