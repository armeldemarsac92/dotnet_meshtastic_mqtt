namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class BrokerServerProfileQueries
{
    public static string GetAll =>
        """
        SELECT
            id AS Id,
            name AS Name,
            host AS Host,
            port AS Port,
            use_tls AS UseTls,
            username AS Username,
            password AS Password,
            default_topic_pattern AS DefaultTopicPattern,
            default_encryption_key_base64 AS DefaultEncryptionKeyBase64,
            downlink_topic AS DownlinkTopic,
            enable_send AS EnableSend,
            is_active AS IsActive,
            created_at_utc AS CreatedAtUtc
        FROM broker_server_profiles
        WHERE workspace_id = @WorkspaceId
        ORDER BY is_active DESC, name COLLATE NOCASE ASC;
        """;

    public static string GetActive =>
        """
        SELECT
            id AS Id,
            name AS Name,
            host AS Host,
            port AS Port,
            use_tls AS UseTls,
            username AS Username,
            password AS Password,
            default_topic_pattern AS DefaultTopicPattern,
            default_encryption_key_base64 AS DefaultEncryptionKeyBase64,
            downlink_topic AS DownlinkTopic,
            enable_send AS EnableSend,
            is_active AS IsActive,
            created_at_utc AS CreatedAtUtc
        FROM broker_server_profiles
        WHERE workspace_id = @WorkspaceId
          AND is_active = 1
        ORDER BY created_at_utc DESC
        LIMIT 1;
        """;

    public static string GetById =>
        """
        SELECT
            id AS Id,
            name AS Name,
            host AS Host,
            port AS Port,
            use_tls AS UseTls,
            username AS Username,
            password AS Password,
            default_topic_pattern AS DefaultTopicPattern,
            default_encryption_key_base64 AS DefaultEncryptionKeyBase64,
            downlink_topic AS DownlinkTopic,
            enable_send AS EnableSend,
            is_active AS IsActive,
            created_at_utc AS CreatedAtUtc
        FROM broker_server_profiles
        WHERE workspace_id = @WorkspaceId
          AND id = @Id;
        """;

    public static string GetSubscriptionIntentsInitialized =>
        """
        SELECT COALESCE(subscription_intents_initialized, 0)
        FROM broker_server_profiles
        WHERE workspace_id = @WorkspaceId
          AND id = @Id
        LIMIT 1;
        """;

    public static string MarkSubscriptionIntentsInitialized =>
        """
        UPDATE broker_server_profiles
        SET subscription_intents_initialized = 1
        WHERE workspace_id = @WorkspaceId
          AND id = @Id;
        """;

    public static string SetExclusiveActive =>
        """
        UPDATE broker_server_profiles
        SET is_active = CASE
            WHEN id = @Id THEN 1
            ELSE 0
        END
        WHERE EXISTS (
            SELECT 1
            FROM broker_server_profiles
            WHERE workspace_id = @WorkspaceId
              AND id = @Id
        )
          AND workspace_id = @WorkspaceId
          AND (id = @Id OR is_active = 1);
        """;

    public static string Upsert =>
        """
        INSERT INTO broker_server_profiles (
            id,
            workspace_id,
            name,
            host,
            port,
            use_tls,
            username,
            password,
            default_topic_pattern,
            default_encryption_key_base64,
            downlink_topic,
            enable_send,
            is_active,
            created_at_utc)
        VALUES (
            @Id,
            @WorkspaceId,
            @Name,
            @Host,
            @Port,
            @UseTls,
            @Username,
            @Password,
            @DefaultTopicPattern,
            @DefaultEncryptionKeyBase64,
            @DownlinkTopic,
            @EnableSend,
            @IsActive,
            @CreatedAtUtc)
        ON CONFLICT(id) DO UPDATE SET
            workspace_id = excluded.workspace_id,
            name = excluded.name,
            host = excluded.host,
            port = excluded.port,
            use_tls = excluded.use_tls,
            username = excluded.username,
            password = excluded.password,
            default_topic_pattern = excluded.default_topic_pattern,
            default_encryption_key_base64 = excluded.default_encryption_key_base64,
            downlink_topic = excluded.downlink_topic,
            enable_send = excluded.enable_send,
            is_active = excluded.is_active;
        """;

    public static string InsertIfNoProfilesExist =>
        """
        INSERT INTO broker_server_profiles (
            id,
            workspace_id,
            name,
            host,
            port,
            use_tls,
            username,
            password,
            default_topic_pattern,
            default_encryption_key_base64,
            downlink_topic,
            enable_send,
            is_active,
            created_at_utc)
        SELECT
            @Id,
            @WorkspaceId,
            @Name,
            @Host,
            @Port,
            @UseTls,
            @Username,
            @Password,
            @DefaultTopicPattern,
            @DefaultEncryptionKeyBase64,
            @DownlinkTopic,
            @EnableSend,
            1,
            @CreatedAtUtc
        WHERE NOT EXISTS (
            SELECT 1
            FROM broker_server_profiles
            WHERE workspace_id = @WorkspaceId
        );
        """;
}
