namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class SavedChannelFilterQueries
{
    public static string GetAll =>
        """
        SELECT
            id AS Id,
            broker_server_profile_id AS BrokerServerProfileId,
            topic_filter AS TopicFilter,
            label AS Label,
            created_at_utc AS CreatedAtUtc,
            updated_at_utc AS UpdatedAtUtc
        FROM saved_channel_filters
        WHERE workspace_id = @WorkspaceId
          AND broker_server_profile_id = @BrokerServerProfileId
        ORDER BY updated_at_utc DESC, topic_filter ASC;
        """;

    public static string Upsert =>
        """
        INSERT INTO saved_channel_filters (
            id,
            workspace_id,
            broker_server_profile_id,
            topic_filter,
            label,
            created_at_utc,
            updated_at_utc)
        VALUES (
            @Id,
            @WorkspaceId,
            @BrokerServerProfileId,
            @TopicFilter,
            @Label,
            @CreatedAtUtc,
            @UpdatedAtUtc)
        ON CONFLICT(workspace_id, broker_server_profile_id, topic_filter) DO UPDATE SET
            label = excluded.label,
            updated_at_utc = excluded.updated_at_utc;
        """;

    public static string Delete =>
        """
        DELETE FROM saved_channel_filters
        WHERE workspace_id = @WorkspaceId
          AND broker_server_profile_id = @BrokerServerProfileId
          AND topic_filter = @TopicFilter;
        """;
}
