using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Meshtastic;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class CollectorNeighborLinkRepository : INeighborLinkRepository
{
    private readonly CollectorChannelResolver _channelResolver;
    private readonly IDbContext _dbContext;

    public CollectorNeighborLinkRepository(
        IDbContext dbContext,
        CollectorChannelResolver channelResolver)
    {
        _dbContext = dbContext;
        _channelResolver = channelResolver;
    }

    public async Task UpsertAsync(
        string workspaceId,
        string brokerServer,
        string? channelKey,
        IReadOnlyList<NeighborLinkRecord> links,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || links.Count == 0)
        {
            return;
        }

        var normalizedWorkspaceId = workspaceId.Trim();
        var observedAtUtc = links.Max(link => link.LastSeenAtUtc);
        var channelId = await _channelResolver.ResolveFromChannelKeyAsync(
            normalizedWorkspaceId,
            brokerServer,
            channelKey,
            observedAtUtc,
            cancellationToken);

        foreach (var link in CanonicalizeLinks(links))
        {
            await _dbContext.ExecuteAsync(
                """
                INSERT INTO collector_neighbor_links (
                    workspace_id,
                    channel_id,
                    source_node_id,
                    target_node_id,
                    snr_db,
                    last_seen_at_utc)
                VALUES (
                    @WorkspaceId,
                    @ChannelId,
                    @SourceNodeId,
                    @TargetNodeId,
                    @SnrDb,
                    @LastSeenAtUtc)
                ON CONFLICT(workspace_id, channel_id, source_node_id, target_node_id) DO UPDATE SET
                    snr_db = CASE
                        WHEN EXCLUDED.last_seen_at_utc >= collector_neighbor_links.last_seen_at_utc
                            THEN COALESCE(EXCLUDED.snr_db, collector_neighbor_links.snr_db)
                        ELSE COALESCE(collector_neighbor_links.snr_db, EXCLUDED.snr_db)
                    END,
                    last_seen_at_utc = CASE
                        WHEN EXCLUDED.last_seen_at_utc >= collector_neighbor_links.last_seen_at_utc
                            THEN EXCLUDED.last_seen_at_utc
                        ELSE collector_neighbor_links.last_seen_at_utc
                    END;
                """,
                new
                {
                    WorkspaceId = normalizedWorkspaceId,
                    ChannelId = channelId,
                    link.SourceNodeId,
                    link.TargetNodeId,
                    link.SnrDb,
                    link.LastSeenAtUtc
                },
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<NeighborLinkRecord>> GetActiveLinksAsync(
        string workspaceId,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return [];
        }

        var responses = await _dbContext.QueryAsync<NeighborLinkSqlResponse>(
            """
            SELECT
                source_node_id AS SourceNodeId,
                target_node_id AS TargetNodeId,
                snr_db AS SnrDb,
                last_seen_at_utc::text AS LastSeenAtUtc
            FROM collector_neighbor_links
            WHERE workspace_id = @WorkspaceId
              AND last_seen_at_utc >= @NotBeforeUtc
            ORDER BY last_seen_at_utc DESC,
                     source_node_id ASC,
                     target_node_id ASC;
            """,
            new
            {
                WorkspaceId = workspaceId.Trim(),
                NotBeforeUtc = notBeforeUtc
            },
            cancellationToken);

        return responses
            .Select(response => new NeighborLinkRecord
            {
                SourceNodeId = response.SourceNodeId,
                TargetNodeId = response.TargetNodeId,
                SnrDb = response.SnrDb,
                LastSeenAtUtc = DateTimeOffset.Parse(response.LastSeenAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind)
            })
            .ToArray();
    }

    private static IReadOnlyList<NeighborLinkRecord> CanonicalizeLinks(IReadOnlyList<NeighborLinkRecord> links)
    {
        var canonicalLinks = new Dictionary<string, NeighborLinkRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link.SourceNodeId) ||
                string.IsNullOrWhiteSpace(link.TargetNodeId) ||
                string.Equals(link.SourceNodeId, link.TargetNodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceNodeId = link.SourceNodeId.Trim();
            var targetNodeId = link.TargetNodeId.Trim();
            var sourceFirst = StringComparer.OrdinalIgnoreCase.Compare(sourceNodeId, targetNodeId) <= 0;
            var canonical = new NeighborLinkRecord
            {
                SourceNodeId = sourceFirst ? sourceNodeId : targetNodeId,
                TargetNodeId = sourceFirst ? targetNodeId : sourceNodeId,
                SnrDb = link.SnrDb,
                LastSeenAtUtc = link.LastSeenAtUtc
            };
            var key = $"{canonical.SourceNodeId}|{canonical.TargetNodeId}";

            if (!canonicalLinks.TryGetValue(key, out var existing))
            {
                canonicalLinks[key] = canonical;
                continue;
            }

            var isIncomingLatest = canonical.LastSeenAtUtc >= existing.LastSeenAtUtc;
            canonicalLinks[key] = new NeighborLinkRecord
            {
                SourceNodeId = existing.SourceNodeId,
                TargetNodeId = existing.TargetNodeId,
                SnrDb = isIncomingLatest
                    ? canonical.SnrDb ?? existing.SnrDb
                    : existing.SnrDb ?? canonical.SnrDb,
                LastSeenAtUtc = isIncomingLatest
                    ? canonical.LastSeenAtUtc
                    : existing.LastSeenAtUtc
            };
        }

        return canonicalLinks.Values.ToArray();
    }
}
