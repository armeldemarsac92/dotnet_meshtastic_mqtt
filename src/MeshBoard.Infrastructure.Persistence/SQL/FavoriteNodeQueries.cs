namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class FavoriteNodeQueries
{
    public static string DeleteFavoriteNode =>
        """
        DELETE FROM favorite_nodes
        WHERE workspace_id = @WorkspaceId
          AND node_id = @NodeId;
        """;

    public static string GetFavoriteNodeByNodeId =>
        """
        SELECT
            id AS Id,
            node_id AS NodeId,
            short_name AS ShortName,
            long_name AS LongName,
            created_at_utc AS CreatedAtUtc
        FROM favorite_nodes
        WHERE workspace_id = @WorkspaceId
          AND node_id = @NodeId;
        """;

    public static string GetFavoriteNodes =>
        """
        SELECT
            id AS Id,
            node_id AS NodeId,
            short_name AS ShortName,
            long_name AS LongName,
            created_at_utc AS CreatedAtUtc
        FROM favorite_nodes
        WHERE workspace_id = @WorkspaceId
        ORDER BY COALESCE(short_name, node_id);
        """;

    public static string UpsertFavoriteNode =>
        """
        INSERT INTO favorite_nodes (
            id,
            workspace_id,
            node_id,
            short_name,
            long_name,
            created_at_utc)
        VALUES (
            @Id,
            @WorkspaceId,
            @NodeId,
            @ShortName,
            @LongName,
            @CreatedAtUtc)
        ON CONFLICT(workspace_id, node_id) DO UPDATE SET
            short_name = excluded.short_name,
            long_name = excluded.long_name;
        """;
}
