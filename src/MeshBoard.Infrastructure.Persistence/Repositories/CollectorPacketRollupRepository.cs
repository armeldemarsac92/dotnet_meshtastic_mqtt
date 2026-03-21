using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Collector;
using MeshBoard.Infrastructure.Persistence.Context;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class CollectorPacketRollupRepository : ICollectorPacketRollupRepository
{
    private readonly IDbContext _dbContext;

    public CollectorPacketRollupRepository(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordObservedMessageAsync(
        CollectorObservedPacketRollupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceId) ||
            request.ChannelId <= 0 ||
            string.IsNullOrWhiteSpace(request.NodeId) ||
            string.IsNullOrWhiteSpace(request.PacketType))
        {
            return;
        }

        var bucketStartUtc = GetBucketStartUtc(request.ObservedAtUtc);
        var parameters = new
        {
            WorkspaceId = request.WorkspaceId.Trim(),
            request.ChannelId,
            NodeId = request.NodeId.Trim(),
            PacketType = request.PacketType.Trim(),
            BucketStartUtc = bucketStartUtc,
            ObservedAtUtc = request.ObservedAtUtc
        };

        await _dbContext.ExecuteAsync(
            """
            INSERT INTO collector_channel_packet_hourly_rollups (
                workspace_id,
                channel_id,
                bucket_start_utc,
                packet_type,
                packet_count,
                first_seen_at_utc,
                last_seen_at_utc)
            VALUES (
                @WorkspaceId,
                @ChannelId,
                @BucketStartUtc,
                @PacketType,
                1,
                @ObservedAtUtc,
                @ObservedAtUtc)
            ON CONFLICT(workspace_id, channel_id, bucket_start_utc, packet_type) DO UPDATE SET
                packet_count = collector_channel_packet_hourly_rollups.packet_count + 1,
                first_seen_at_utc = LEAST(
                    collector_channel_packet_hourly_rollups.first_seen_at_utc,
                    EXCLUDED.first_seen_at_utc),
                last_seen_at_utc = GREATEST(
                    collector_channel_packet_hourly_rollups.last_seen_at_utc,
                    EXCLUDED.last_seen_at_utc);
            """,
            parameters,
            cancellationToken);

        await _dbContext.ExecuteAsync(
            """
            INSERT INTO collector_node_packet_hourly_rollups (
                workspace_id,
                channel_id,
                bucket_start_utc,
                node_id,
                packet_type,
                packet_count,
                first_seen_at_utc,
                last_seen_at_utc)
            VALUES (
                @WorkspaceId,
                @ChannelId,
                @BucketStartUtc,
                @NodeId,
                @PacketType,
                1,
                @ObservedAtUtc,
                @ObservedAtUtc)
            ON CONFLICT(workspace_id, channel_id, bucket_start_utc, node_id, packet_type) DO UPDATE SET
                packet_count = collector_node_packet_hourly_rollups.packet_count + 1,
                first_seen_at_utc = LEAST(
                    collector_node_packet_hourly_rollups.first_seen_at_utc,
                    EXCLUDED.first_seen_at_utc),
                last_seen_at_utc = GREATEST(
                    collector_node_packet_hourly_rollups.last_seen_at_utc,
                    EXCLUDED.last_seen_at_utc);
            """,
            parameters,
            cancellationToken);
    }

    private static DateTimeOffset GetBucketStartUtc(DateTimeOffset observedAtUtc)
    {
        var utc = observedAtUtc.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
    }
}
