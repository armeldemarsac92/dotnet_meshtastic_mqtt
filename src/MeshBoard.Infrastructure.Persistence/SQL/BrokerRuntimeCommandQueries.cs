namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class BrokerRuntimeCommandQueries
{
    public static string Insert =>
        """
        INSERT INTO broker_runtime_commands (
            id,
            workspace_id,
            command_type,
            topic,
            payload,
            topic_filter,
            status,
            attempt_count,
            created_at_utc,
            available_at_utc,
            leased_by,
            leased_at_utc,
            lease_expires_at_utc,
            completed_at_utc,
            failed_at_utc,
            last_error
        )
        VALUES (
            @Id,
            @WorkspaceId,
            @CommandType,
            @Topic,
            @Payload,
            @TopicFilter,
            @Status,
            @AttemptCount,
            @CreatedAtUtc,
            @AvailableAtUtc,
            NULL,
            NULL,
            NULL,
            NULL,
            NULL,
            NULL
        );
        """;

    public static string SelectLeaseableIds =>
        """
        SELECT id
        FROM broker_runtime_commands
        WHERE
            (status = @PendingStatus AND available_at_utc <= @NowUtc)
            OR (status = @LeasedStatus AND lease_expires_at_utc IS NOT NULL AND lease_expires_at_utc <= @NowUtc)
        ORDER BY created_at_utc
        LIMIT @BatchSize;
        """;

    public static string LeaseByIds =>
        """
        UPDATE broker_runtime_commands
        SET
            status = @LeasedStatus,
            attempt_count = attempt_count + 1,
            leased_by = @LeasedBy,
            leased_at_utc = @LeasedAtUtc,
            lease_expires_at_utc = @LeaseExpiresAtUtc,
            last_error = NULL,
            failed_at_utc = NULL
        WHERE id IN @Ids;
        """;

    public static string GetByIds =>
        """
        SELECT
            id AS Id,
            workspace_id AS WorkspaceId,
            command_type AS CommandType,
            status AS Status,
            topic AS Topic,
            payload AS Payload,
            topic_filter AS TopicFilter,
            attempt_count AS AttemptCount,
            created_at_utc AS CreatedAtUtc,
            available_at_utc AS AvailableAtUtc,
            leased_at_utc AS LeasedAtUtc,
            lease_expires_at_utc AS LeaseExpiresAtUtc,
            completed_at_utc AS CompletedAtUtc,
            failed_at_utc AS FailedAtUtc,
            last_error AS LastError
        FROM broker_runtime_commands
        WHERE id IN @Ids
        ORDER BY created_at_utc;
        """;

    public static string GetRecentByWorkspace =>
        """
        SELECT
            id AS Id,
            workspace_id AS WorkspaceId,
            command_type AS CommandType,
            status AS Status,
            topic AS Topic,
            payload AS Payload,
            topic_filter AS TopicFilter,
            attempt_count AS AttemptCount,
            created_at_utc AS CreatedAtUtc,
            available_at_utc AS AvailableAtUtc,
            leased_at_utc AS LeasedAtUtc,
            lease_expires_at_utc AS LeaseExpiresAtUtc,
            completed_at_utc AS CompletedAtUtc,
            failed_at_utc AS FailedAtUtc,
            last_error AS LastError
        FROM broker_runtime_commands
        WHERE workspace_id = @WorkspaceId
        ORDER BY created_at_utc DESC
        LIMIT @Take;
        """;

    public static string GetWorkspaceIdById =>
        """
        SELECT workspace_id
        FROM broker_runtime_commands
        WHERE id = @Id;
        """;

    public static string MarkCompleted =>
        """
        UPDATE broker_runtime_commands
        SET
            status = @CompletedStatus,
            completed_at_utc = @CompletedAtUtc,
            leased_by = NULL,
            leased_at_utc = NULL,
            lease_expires_at_utc = NULL,
            last_error = NULL
        WHERE id = @Id;
        """;

    public static string MarkPending =>
        """
        UPDATE broker_runtime_commands
        SET
            status = @PendingStatus,
            available_at_utc = @AvailableAtUtc,
            leased_by = NULL,
            leased_at_utc = NULL,
            lease_expires_at_utc = NULL,
            last_error = @LastError
        WHERE id = @Id;
        """;

    public static string MarkFailed =>
        """
        UPDATE broker_runtime_commands
        SET
            status = @FailedStatus,
            failed_at_utc = @FailedAtUtc,
            leased_by = NULL,
            leased_at_utc = NULL,
            lease_expires_at_utc = NULL,
            last_error = @LastError
        WHERE id = @Id;
        """;
}
