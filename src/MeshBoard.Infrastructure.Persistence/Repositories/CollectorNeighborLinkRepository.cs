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
        string brokerServer,
        string? channelKey,
        IReadOnlyList<NeighborLinkRecord> links,
        CancellationToken cancellationToken = default)
    {
        if (links.Count == 0)
        {
            return;
        }

        var observedAtUtc = links.Max(link => link.LastSeenAtUtc);
        var resolvedChannel = await _channelResolver.ResolveFromChannelKeyAsync(
            brokerServer,
            channelKey,
            observedAtUtc,
            cancellationToken);

        foreach (var link in CanonicalizeLinks(links))
        {
            await _dbContext.ExecuteAsync(
                """
                INSERT INTO collector_neighbor_links (
                    channel_id,
                    source_node_id,
                    target_node_id,
                    snr_db,
                    last_seen_at_utc)
                VALUES (
                    @ChannelId,
                    @SourceNodeId,
                    @TargetNodeId,
                    @SnrDb,
                    @LastSeenAtUtc)
                ON CONFLICT(channel_id, source_node_id, target_node_id) DO UPDATE SET
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
                    ChannelId = resolvedChannel.ChannelId,
                    link.SourceNodeId,
                    link.TargetNodeId,
                    link.SnrDb,
                    link.LastSeenAtUtc
                },
                cancellationToken);

            await RecordHourlyRollupAsync(resolvedChannel.ChannelId, link, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<NeighborLinkRecord>> GetActiveLinksAsync(
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<NeighborLinkSqlResponse>(
            """
            SELECT
                source_node_id AS SourceNodeId,
                target_node_id AS TargetNodeId,
                snr_db AS SnrDb,
                last_seen_at_utc::text AS LastSeenAtUtc
            FROM collector_neighbor_links
            WHERE last_seen_at_utc >= @NotBeforeUtc
            ORDER BY last_seen_at_utc DESC,
                     source_node_id ASC,
                     target_node_id ASC;
            """,
            new { NotBeforeUtc = notBeforeUtc },
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

    private async Task RecordHourlyRollupAsync(
        long channelId,
        NeighborLinkRecord link,
        CancellationToken cancellationToken)
    {
        var bucketStartUtc = GetBucketStartUtc(link.LastSeenAtUtc);
        var hasSnr = link.SnrDb.HasValue;
        var snrValue = link.SnrDb ?? 0f;

        await _dbContext.ExecuteAsync(
            """
            INSERT INTO collector_neighbor_link_hourly_rollups (
                channel_id,
                bucket_start_utc,
                source_node_id,
                target_node_id,
                observation_count,
                snr_sample_count,
                snr_sum_db,
                max_snr_db,
                last_snr_db,
                first_seen_at_utc,
                last_seen_at_utc)
            VALUES (
                @ChannelId,
                @BucketStartUtc,
                @SourceNodeId,
                @TargetNodeId,
                1,
                @SnrSampleCount,
                @SnrSumDb,
                @MaxSnrDb,
                @LastSnrDb,
                @ObservedAtUtc,
                @ObservedAtUtc)
            ON CONFLICT(channel_id, bucket_start_utc, source_node_id, target_node_id) DO UPDATE SET
                observation_count = collector_neighbor_link_hourly_rollups.observation_count + 1,
                snr_sample_count = collector_neighbor_link_hourly_rollups.snr_sample_count + EXCLUDED.snr_sample_count,
                snr_sum_db = collector_neighbor_link_hourly_rollups.snr_sum_db + EXCLUDED.snr_sum_db,
                max_snr_db = CASE
                    WHEN EXCLUDED.snr_sample_count = 0 THEN collector_neighbor_link_hourly_rollups.max_snr_db
                    WHEN collector_neighbor_link_hourly_rollups.max_snr_db IS NULL THEN EXCLUDED.max_snr_db
                    ELSE GREATEST(collector_neighbor_link_hourly_rollups.max_snr_db, EXCLUDED.max_snr_db)
                END,
                last_snr_db = CASE
                    WHEN EXCLUDED.last_seen_at_utc >= collector_neighbor_link_hourly_rollups.last_seen_at_utc
                        THEN COALESCE(EXCLUDED.last_snr_db, collector_neighbor_link_hourly_rollups.last_snr_db)
                    ELSE collector_neighbor_link_hourly_rollups.last_snr_db
                END,
                first_seen_at_utc = LEAST(
                    collector_neighbor_link_hourly_rollups.first_seen_at_utc,
                    EXCLUDED.first_seen_at_utc),
                last_seen_at_utc = GREATEST(
                    collector_neighbor_link_hourly_rollups.last_seen_at_utc,
                    EXCLUDED.last_seen_at_utc);
            """,
            new
            {
                ChannelId = channelId,
                BucketStartUtc = bucketStartUtc,
                link.SourceNodeId,
                link.TargetNodeId,
                SnrSampleCount = hasSnr ? 1 : 0,
                SnrSumDb = hasSnr ? snrValue : 0,
                MaxSnrDb = hasSnr ? link.SnrDb : null,
                LastSnrDb = hasSnr ? link.SnrDb : null,
                ObservedAtUtc = link.LastSeenAtUtc
            },
            cancellationToken);
    }

    private static DateTimeOffset GetBucketStartUtc(DateTimeOffset observedAtUtc)
    {
        var utc = observedAtUtc.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}
