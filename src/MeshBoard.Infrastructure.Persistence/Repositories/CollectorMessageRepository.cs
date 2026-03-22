using System.Globalization;
using System.Text;
using Dapper;
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

    private const string SelectColumns =
        """
        SELECT
            m.id::text AS Id,
            s.server_address AS BrokerServer,
            m.topic AS Topic,
            m.packet_type AS PacketType,
            m.from_node_id AS FromNodeId,
            n.short_name AS FromNodeShortName,
            n.long_name AS FromNodeLongName,
            m.to_node_id AS ToNodeId,
            m.payload_preview AS PayloadPreview,
            CASE WHEN m.is_private THEN 1 ELSE 0 END AS IsPrivate,
            m.received_at_utc::text AS ReceivedAtUtc
        """;

    private const string BaseFromClause =
        """
        FROM collector_messages m
        INNER JOIN collector_channels c
            ON c.id = m.channel_id
        INNER JOIN collector_servers s
            ON s.id = c.server_id
        LEFT JOIN collector_nodes n
            ON n.server_id = s.id
           AND n.node_id = m.from_node_id
        """;

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
        _logger.LogDebug("Attempting to add collector observed message from node: {NodeId}", request.FromNodeId);

        var resolvedChannel = await _channelResolver.ResolveFromTopicAsync(
            request.BrokerServer,
            request.Topic,
            request.ReceivedAtUtc,
            cancellationToken);

        var rowsAffected = await _dbContext.ExecuteAsync(
            """
            INSERT INTO collector_messages (
                id,
                channel_id,
                message_key,
                topic,
                packet_type,
                from_node_id,
                to_node_id,
                payload_preview,
                is_private,
                received_at_utc)
            VALUES (
                @Id,
                @ChannelId,
                @MessageKey,
                @Topic,
                @PacketType,
                @FromNodeId,
                @ToNodeId,
                @PayloadPreview,
                @IsPrivate,
                @ReceivedAtUtc)
            ON CONFLICT(message_key) DO NOTHING;
            """,
            new
            {
                Id = Guid.NewGuid(),
                ChannelId = resolvedChannel.ChannelId,
                request.MessageKey,
                request.Topic,
                request.PacketType,
                request.FromNodeId,
                request.ToNodeId,
                request.PayloadPreview,
                request.IsPrivate,
                request.ReceivedAtUtc
            },
            cancellationToken);

        if (rowsAffected > 0)
        {
            await _collectorPacketRollupRepository.RecordObservedMessageAsync(
                new CollectorObservedPacketRollupRequest
                {
                    ChannelId = resolvedChannel.ChannelId,
                    NodeId = request.FromNodeId,
                    PacketType = request.PacketType,
                    ObservedAtUtc = request.ReceivedAtUtc
                },
                cancellationToken);
        }

        return rowsAffected > 0;
    }

    public Task<int> CountAsync(MessageQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to count collector messages");

        return _dbContext.QueryFirstOrDefaultAsync<int>(
            BuildMessagesPageSql("SELECT COUNT(1)", query, includeOrderAndPaging: false),
            CreateQueryParameters(query),
            cancellationToken);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to delete collector messages older than {CutoffUtc}", cutoffUtc);

        return _dbContext.ExecuteAsync(
            """
            DELETE FROM collector_messages
            WHERE received_at_utc < @CutoffUtc;
            """,
            new { CutoffUtc = cutoffUtc },
            cancellationToken);
    }

    public Task<int> CountBySenderAsync(string senderNodeId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to count collector messages by sender {SenderNodeId}", senderNodeId);

        return _dbContext.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COUNT(1)
            FROM collector_messages
            WHERE from_node_id = @SenderNodeId;
            """,
            new { SenderNodeId = senderNodeId },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetPageAsync(
        MessageQuery query,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch collector messages page with offset {Offset} and take {Take}",
            offset,
            take);

        var parameters = CreateQueryParameters(query);
        parameters.Add("Offset", offset);
        parameters.Add("Take", take);

        var responses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            BuildMessagesPageSql(SelectColumns, query, includeOrderAndPaging: true),
            parameters,
            cancellationToken);

        return responses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            $"{SelectColumns}{Environment.NewLine}{BaseFromClause}{Environment.NewLine}" +
            """
            WHERE 1 = 1
            ORDER BY m.received_at_utc DESC, m.id DESC
            LIMIT @Take;
            """,
            new { Take = take },
            cancellationToken);

        return responses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentByBrokerAsync(
        string brokerServer,
        int take,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            $"{SelectColumns}{Environment.NewLine}{BaseFromClause}{Environment.NewLine}" +
            """
            WHERE s.server_address = @BrokerServer
            ORDER BY m.received_at_utc DESC, m.id DESC
            LIMIT @Take;
            """,
            new
            {
                BrokerServer = brokerServer.Trim(),
                Take = take
            },
            cancellationToken);

        return responses.MapToMessages();
    }

    public async Task<ChannelSummary> GetChannelSummaryAsync(
        string region,
        string channel,
        CancellationToken cancellationToken = default)
    {
        var response = await _dbContext.QueryFirstOrDefaultAsync<ChannelSummarySqlResponse>(
            """
            SELECT
                COUNT(1) AS PacketCount,
                COUNT(DISTINCT m.from_node_id) AS UniqueSenderCount,
                COALESCE(SUM(
                    CASE
                        WHEN m.packet_type = ANY(@OpaquePacketTypes) THEN 0
                        ELSE 1
                    END), 0) AS DecodedPacketCount,
                MAX(m.received_at_utc)::text AS LastSeenAtUtc,
                string_agg(DISTINCT s.server_address, ',') AS BrokerServersCsv
            FROM collector_messages m
            INNER JOIN collector_channels c
                ON c.id = m.channel_id
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

        if (response is null)
        {
            return new ChannelSummary();
        }

        return new ChannelSummary
        {
            PacketCount = response.PacketCount,
            UniqueSenderCount = response.UniqueSenderCount,
            DecodedPacketCount = response.DecodedPacketCount,
            LastSeenAtUtc = ParseNullableDateTimeOffset(response.LastSeenAtUtc),
            ObservedBrokerServers = ParseBrokerServers(response.BrokerServersCsv)
        };
    }

    public Task<int> CountByChannelAsync(
        string region,
        string channel,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.QueryFirstOrDefaultAsync<int>(
            """
            SELECT COUNT(1)
            FROM collector_messages m
            INNER JOIN collector_channels c
                ON c.id = m.channel_id
            WHERE c.region = @Region
              AND c.channel_name = @ChannelName;
            """,
            CreateChannelQueryParameters(region, channel),
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
                m.from_node_id AS NodeId,
                COALESCE(NULLIF(n.short_name, ''), NULLIF(n.long_name, ''), m.from_node_id) AS DisplayName,
                COUNT(1) AS PacketCount
            FROM collector_messages m
            INNER JOIN collector_channels c
                ON c.id = m.channel_id
            LEFT JOIN collector_nodes n
                ON n.server_id = c.server_id
               AND n.node_id = m.from_node_id
            WHERE c.region = @Region
              AND c.channel_name = @ChannelName
            GROUP BY
                m.from_node_id,
                COALESCE(NULLIF(n.short_name, ''), NULLIF(n.long_name, ''), m.from_node_id)
            ORDER BY COUNT(1) DESC,
                     DisplayName ASC,
                     m.from_node_id ASC
            LIMIT @Take;
            """,
            CreateChannelQueryParameters(region, channel, take),
            cancellationToken);

        return responses
            .Select(response => new ChannelTopNode
            {
                NodeId = response.NodeId,
                DisplayName = response.DisplayName,
                PacketCount = response.PacketCount
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetPageByChannelAsync(
        string region,
        string channel,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            $"{SelectColumns}{Environment.NewLine}{BaseFromClause}{Environment.NewLine}" +
            """
            WHERE c.region = @Region
              AND c.channel_name = @ChannelName
            ORDER BY m.received_at_utc DESC, m.id DESC
            LIMIT @Take OFFSET @Offset;
            """,
            CreateChannelQueryParameters(region, channel, take, offset),
            cancellationToken);

        return responses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentByChannelAsync(
        string region,
        string channel,
        int take,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            $"{SelectColumns}{Environment.NewLine}{BaseFromClause}{Environment.NewLine}" +
            """
            WHERE c.region = @Region
              AND c.channel_name = @ChannelName
            ORDER BY m.received_at_utc DESC, m.id DESC
            LIMIT @Take;
            """,
            CreateChannelQueryParameters(region, channel, take),
            cancellationToken);

        return responses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentBySenderAsync(
        string senderNodeId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            $"{SelectColumns}{Environment.NewLine}{BaseFromClause}{Environment.NewLine}" +
            """
            WHERE m.from_node_id = @SenderNodeId
            ORDER BY m.received_at_utc DESC, m.id DESC
            LIMIT @Take;
            """,
            new
            {
                SenderNodeId = senderNodeId,
                Take = take
            },
            cancellationToken);

        return responses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetPageBySenderAsync(
        string senderNodeId,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            $"{SelectColumns}{Environment.NewLine}{BaseFromClause}{Environment.NewLine}" +
            """
            WHERE m.from_node_id = @SenderNodeId
            ORDER BY m.received_at_utc DESC, m.id DESC
            LIMIT @Take OFFSET @Offset;
            """,
            new
            {
                SenderNodeId = senderNodeId,
                Offset = offset,
                Take = take
            },
            cancellationToken);

        return responses.MapToMessages();
    }

    private object CreateChannelQueryParameters(string region, string channel, int? take = null)
    {
        return new
        {
            Region = region.Trim(),
            ChannelName = channel.Trim(),
            Take = take
        };
    }

    private object CreateChannelQueryParameters(string region, string channel, int? take, int offset)
    {
        return new
        {
            Region = region.Trim(),
            ChannelName = channel.Trim(),
            Take = take,
            Offset = offset
        };
    }

    private static DateTimeOffset? ParseNullableDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedValue)
            ? parsedValue
            : null;
    }

    private static IReadOnlyCollection<string> ParseBrokerServers(string? brokerServersCsv)
    {
        if (string.IsNullOrWhiteSpace(brokerServersCsv))
        {
            return [];
        }

        return brokerServersCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(server => server, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildMessagesPageSql(string selectClause, MessageQuery query, bool includeOrderAndPaging)
    {
        var sqlBuilder = new StringBuilder(selectClause)
            .AppendLine()
            .AppendLine(BaseFromClause)
            .AppendLine("WHERE 1 = 1");

        if (!string.IsNullOrWhiteSpace(query.BrokerServer))
        {
            sqlBuilder.AppendLine("  AND s.server_address = @BrokerServer");
        }

        if (!string.IsNullOrWhiteSpace(query.PacketType))
        {
            sqlBuilder.AppendLine("  AND m.packet_type = @PacketType");
        }

        switch (query.Visibility)
        {
            case MessageVisibilityFilter.DecodedOnly:
                sqlBuilder.AppendLine("  AND NOT (m.packet_type = ANY(@OpaquePacketTypes))");
                break;
            case MessageVisibilityFilter.OpaqueOnly:
                sqlBuilder.AppendLine("  AND m.packet_type = ANY(@OpaquePacketTypes)");
                break;
            case MessageVisibilityFilter.PrivateOnly:
                sqlBuilder.AppendLine("  AND m.is_private");
                break;
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            sqlBuilder.AppendLine(
                """
                  AND (
                        m.from_node_id ILIKE @SearchPattern
                     OR COALESCE(n.short_name, '') ILIKE @SearchPattern
                     OR COALESCE(n.long_name, '') ILIKE @SearchPattern
                     OR COALESCE(m.to_node_id, '') ILIKE @SearchPattern
                     OR m.packet_type ILIKE @SearchPattern
                     OR m.payload_preview ILIKE @SearchPattern
                  )
                """);
        }

        if (includeOrderAndPaging)
        {
            sqlBuilder.AppendLine("ORDER BY m.received_at_utc DESC, m.id DESC");
            sqlBuilder.AppendLine("LIMIT @Take OFFSET @Offset;");
        }
        else
        {
            sqlBuilder.Append(';');
        }

        return sqlBuilder.ToString();
    }

    private DynamicParameters CreateQueryParameters(MessageQuery query)
    {
        var parameters = new DynamicParameters();
        var normalizedSearchText = query.SearchText.Trim();

        parameters.Add("BrokerServer", query.BrokerServer.Trim());
        parameters.Add("PacketType", query.PacketType.Trim());
        parameters.Add("SearchPattern", $"%{normalizedSearchText}%");
        parameters.Add("OpaquePacketTypes", OpaquePacketTypes.ToArray());
        return parameters;
    }
}
