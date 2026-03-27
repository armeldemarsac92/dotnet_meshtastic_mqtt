using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class CollectorMessageRepository : IMessageRepository
{
    private static readonly IReadOnlyCollection<string> OpaquePacketTypes = ["Encrypted Packet", "Unknown Packet", "Legacy Packet"];

    private readonly CollectorChannelResolver _channelResolver;
    private readonly ICollectorPacketRollupRepository _collectorPacketRollupRepository;
    private readonly IDbContext _dbContext;
    private readonly ILogger<CollectorMessageRepository> _logger;

    public CollectorMessageRepository(
        IDbContext dbContext,
        CollectorChannelResolver channelResolver,
        ICollectorPacketRollupRepository collectorPacketRollupRepository,
        ILogger<CollectorMessageRepository> logger)
    {
        _dbContext = dbContext;
        _channelResolver = channelResolver;
        _collectorPacketRollupRepository = collectorPacketRollupRepository;
        _logger = logger;
    }

    public async Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to record collector packet rollup from node: {NodeId}",
            request.FromNodeId);

        var resolvedChannel = await _channelResolver.ResolveFromTopicAsync(
            request.BrokerServer,
            request.Topic,
            request.ReceivedAtUtc,
            cancellationToken);

        await _collectorPacketRollupRepository.RecordObservedMessageAsync(
            request.ToCollectorObservedPacketRollupRequest(resolvedChannel.ChannelId),
            cancellationToken);

        return true;
    }

    public Task<int> CountAsync(MessageQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collector message history is disabled; returning zero message count.");
        return Task.FromResult(0);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Collector message history is disabled; no collector rows need pruning before {CutoffUtc}",
            cutoffUtc);

        return Task.FromResult(0);
    }

    public Task<int> CountBySenderAsync(string senderNodeId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to count collector packet rollups by sender {SenderNodeId}",
            senderNodeId);

        return _dbContext.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COALESCE(SUM(packet_count), 0)
            FROM collector_node_packet_hourly_rollups
            WHERE node_id = @SenderNodeId;
            """,
            new { SenderNodeId = senderNodeId.Trim() },
            cancellationToken);
    }

    public Task<IReadOnlyCollection<MessageSummary>> GetPageAsync(
        MessageQuery query,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collector message history is disabled; returning an empty page.");
        return Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }

    public Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collector message history is disabled; returning no recent messages.");
        return Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }

    public Task<IReadOnlyCollection<MessageSummary>> GetRecentByBrokerAsync(
        string brokerServer,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Collector message history is disabled; returning no recent messages for broker {BrokerServer}.",
            brokerServer);
        return Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }

    public async Task<ChannelSummary> GetChannelSummaryAsync(
        string region,
        string channel,
        CancellationToken cancellationToken = default)
    {
        var response = await _dbContext.QueryFirstOrDefaultAsync<ChannelSummarySqlResponse>(
            """
            SELECT
                COALESCE(SUM(r.packet_count), 0) AS PacketCount,
                (
                    SELECT COUNT(DISTINCT nr.node_id)
                    FROM collector_node_packet_hourly_rollups nr
                    INNER JOIN collector_channels nc
                        ON nc.id = nr.channel_id
                    WHERE nc.region = @Region
                      AND nc.channel_name = @ChannelName
                ) AS UniqueSenderCount,
                COALESCE(SUM(
                    CASE
                        WHEN r.packet_type = ANY(@OpaquePacketTypes) THEN 0
                        ELSE r.packet_count
                    END), 0) AS DecodedPacketCount,
                MAX(r.last_seen_at_utc)::text AS LastSeenAtUtc,
                string_agg(DISTINCT s.server_address, ',') AS BrokerServersCsv
            FROM collector_channel_packet_hourly_rollups r
            INNER JOIN collector_channels c
                ON c.id = r.channel_id
            INNER JOIN collector_servers s
                ON s.id = c.server_id
            WHERE c.region = @Region
              AND c.channel_name = @ChannelName;
            """,
            new
            {
                Region = region.Trim(),
                ChannelName = channel.Trim(),
                OpaquePacketTypes
            },
            cancellationToken);

        return response.ToChannelSummary();
    }

    public Task<int> CountByChannelAsync(
        string region,
        string channel,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COALESCE(SUM(r.packet_count), 0)
            FROM collector_channel_packet_hourly_rollups r
            INNER JOIN collector_channels c
                ON c.id = r.channel_id
            WHERE c.region = @Region
              AND c.channel_name = @ChannelName;
            """,
            new
            {
                Region = region.Trim(),
                ChannelName = channel.Trim()
            },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannelAsync(
        string region,
        string channel,
        int take,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<ChannelTopNodeSqlResponse>(
            """
            SELECT
                r.node_id AS NodeId,
                COALESCE(NULLIF(n.short_name, ''), NULLIF(n.long_name, ''), r.node_id) AS DisplayName,
                SUM(r.packet_count) AS PacketCount
            FROM collector_node_packet_hourly_rollups r
            INNER JOIN collector_channels c
                ON c.id = r.channel_id
            LEFT JOIN collector_nodes n
                ON n.server_id = c.server_id
               AND n.node_id = r.node_id
            WHERE c.region = @Region
              AND c.channel_name = @ChannelName
            GROUP BY
                r.node_id,
                COALESCE(NULLIF(n.short_name, ''), NULLIF(n.long_name, ''), r.node_id)
            ORDER BY SUM(r.packet_count) DESC,
                     DisplayName ASC,
                     r.node_id ASC
            LIMIT @Take;
            """,
            new
            {
                Region = region.Trim(),
                ChannelName = channel.Trim(),
                Take = take
            },
            cancellationToken);

        return responses.MapToChannelTopNodes();
    }

    public Task<IReadOnlyCollection<MessageSummary>> GetPageByChannelAsync(
        string region,
        string channel,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Collector message history is disabled; returning no paged channel messages for {Region}/{Channel}.",
            region,
            channel);
        return Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }

    public Task<IReadOnlyCollection<MessageSummary>> GetRecentByChannelAsync(
        string region,
        string channel,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Collector message history is disabled; returning no recent channel messages for {Region}/{Channel}.",
            region,
            channel);
        return Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }

    public Task<IReadOnlyCollection<MessageSummary>> GetRecentBySenderAsync(
        string senderNodeId,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Collector message history is disabled; returning no recent sender messages for {SenderNodeId}.",
            senderNodeId);
        return Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }

    public Task<IReadOnlyCollection<MessageSummary>> GetPageBySenderAsync(
        string senderNodeId,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Collector message history is disabled; returning no paged sender messages for {SenderNodeId}.",
            senderNodeId);
        return Task.FromResult<IReadOnlyCollection<MessageSummary>>([]);
    }
}
