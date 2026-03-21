namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class NeighborLinkQueries
{
    public static string UpsertNeighborLink =>
        """
        INSERT INTO neighbor_links (
            workspace_id,
            source_node_id,
            target_node_id,
            snr_db,
            last_seen_at_utc)
        VALUES (
            @WorkspaceId,
            @SourceNodeId,
            @TargetNodeId,
            @SnrDb,
            @LastSeenAtUtc)
        ON CONFLICT(workspace_id, source_node_id, target_node_id) DO UPDATE SET
            snr_db = CASE
                WHEN excluded.last_seen_at_utc >= neighbor_links.last_seen_at_utc
                    THEN COALESCE(excluded.snr_db, neighbor_links.snr_db)
                ELSE COALESCE(neighbor_links.snr_db, excluded.snr_db)
            END,
            last_seen_at_utc = CASE
                WHEN excluded.last_seen_at_utc >= neighbor_links.last_seen_at_utc
                    THEN excluded.last_seen_at_utc
                ELSE neighbor_links.last_seen_at_utc
            END;
        """;

    public static string SelectActiveNeighborLinks =>
        """
        SELECT
            source_node_id AS SourceNodeId,
            target_node_id AS TargetNodeId,
            snr_db AS SnrDb,
            last_seen_at_utc AS LastSeenAtUtc
        FROM neighbor_links
        WHERE workspace_id = @WorkspaceId
          AND last_seen_at_utc >= @NotBeforeUtc
        ORDER BY last_seen_at_utc DESC,
                 source_node_id COLLATE NOCASE ASC,
                 target_node_id COLLATE NOCASE ASC;
        """;
}
