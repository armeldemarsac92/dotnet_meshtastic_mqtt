namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class WorkspaceRuntimeStatusQueries
{
    public static string GetByWorkspaceId =>
        """
        SELECT
            workspace_id AS WorkspaceId,
            active_server_profile_id AS ActiveServerProfileId,
            active_server_name AS ActiveServerName,
            active_server_address AS ActiveServerAddress,
            is_connected AS IsConnected,
            last_status_message AS LastStatusMessage,
            topic_filters_json AS TopicFiltersJson,
            updated_at_utc AS UpdatedAtUtc
        FROM workspace_runtime_status
        WHERE workspace_id = @WorkspaceId
        LIMIT 1;
        """;

    public static string Upsert =>
        """
        INSERT INTO workspace_runtime_status (
            workspace_id,
            active_server_profile_id,
            active_server_name,
            active_server_address,
            is_connected,
            last_status_message,
            topic_filters_json,
            updated_at_utc
        )
        VALUES (
            @WorkspaceId,
            @ActiveServerProfileId,
            @ActiveServerName,
            @ActiveServerAddress,
            @IsConnected,
            @LastStatusMessage,
            @TopicFiltersJson,
            @UpdatedAtUtc
        )
        ON CONFLICT(workspace_id) DO UPDATE SET
            active_server_profile_id = excluded.active_server_profile_id,
            active_server_name = excluded.active_server_name,
            active_server_address = excluded.active_server_address,
            is_connected = excluded.is_connected,
            last_status_message = excluded.last_status_message,
            topic_filters_json = excluded.topic_filters_json,
            updated_at_utc = excluded.updated_at_utc;
        """;
}
