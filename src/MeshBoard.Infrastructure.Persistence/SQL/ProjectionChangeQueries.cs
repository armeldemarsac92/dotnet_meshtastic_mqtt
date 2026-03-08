namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class ProjectionChangeQueries
{
    public static string Insert =>
        """
        INSERT INTO projection_change_log (
            workspace_id,
            change_kind,
            entity_key,
            occurred_at_utc)
        VALUES (
            @WorkspaceId,
            @ChangeKind,
            @EntityKey,
            @OccurredAtUtc);
        """;

    public static string GetChangesAfter =>
        """
        SELECT
            id AS Id,
            workspace_id AS WorkspaceId,
            change_kind AS ChangeKind,
            entity_key AS EntityKey,
            occurred_at_utc AS OccurredAtUtc
        FROM projection_change_log
        WHERE id > @LastSeenId
        ORDER BY id ASC
        LIMIT @Take;
        """;

    public static string GetLatestId =>
        """
        SELECT COALESCE(MAX(id), 0)
        FROM projection_change_log;
        """;

    public static string DeleteOlderThan =>
        """
        DELETE FROM projection_change_log
        WHERE occurred_at_utc < @CutoffUtc;
        """;
}
