namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class ProductTopicPresetQueries
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
            NULL AS EncryptionKeyBase64,
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
            NULL AS EncryptionKeyBase64,
            is_default AS IsDefault,
            created_at_utc AS CreatedAtUtc
        FROM topic_presets
        WHERE workspace_id = @WorkspaceId
          AND broker_server = @BrokerServer
        ORDER BY is_default DESC, name ASC;
        """;

    public static string UpsertTopicPreset =>
        """
        INSERT INTO topic_presets (
            id,
            workspace_id,
            broker_server,
            name,
            topic_pattern,
            is_default,
            created_at_utc)
        VALUES (
            @Id,
            @WorkspaceId,
            @BrokerServer,
            @Name,
            @TopicPattern,
            @IsDefault,
            @CreatedAtUtc)
        ON CONFLICT(workspace_id, broker_server, topic_pattern) DO UPDATE SET
            name = excluded.name,
            is_default = excluded.is_default;
        """;
}
