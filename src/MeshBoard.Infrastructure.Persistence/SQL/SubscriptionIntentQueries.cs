namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class SubscriptionIntentQueries
{
    public static string GetSubscriptionIntents =>
        """
        SELECT
            broker_server_profile_id AS BrokerServerProfileId,
            topic_filter AS TopicFilter,
            created_at_utc AS CreatedAtUtc
        FROM subscription_intents
        WHERE workspace_id = @WorkspaceId
          AND broker_server_profile_id = @BrokerServerProfileId
        ORDER BY created_at_utc ASC, topic_filter ASC;
        """;

    public static string InsertSubscriptionIntent =>
        """
        INSERT OR IGNORE INTO subscription_intents (
            workspace_id,
            broker_server_profile_id,
            topic_filter,
            created_at_utc)
        VALUES (
            @WorkspaceId,
            @BrokerServerProfileId,
            @TopicFilter,
            @CreatedAtUtc);
        """;

    public static string DeleteSubscriptionIntent =>
        """
        DELETE FROM subscription_intents
        WHERE workspace_id = @WorkspaceId
          AND broker_server_profile_id = @BrokerServerProfileId
          AND topic_filter = @TopicFilter;
        """;
}
