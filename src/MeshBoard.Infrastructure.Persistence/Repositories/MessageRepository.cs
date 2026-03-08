using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Messages;
using MeshBoard.Contracts.Topics;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class MessageRepository : IMessageRepository
{
    private static readonly IReadOnlyCollection<string> OpaquePacketTypes = ["Encrypted Packet", "Unknown Packet", "Legacy Packet"];

    private readonly IDbContext _dbContext;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(IDbContext dbContext, ILogger<MessageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to add observed message from node: {NodeId}", request.FromNodeId);

        var rowsAffected = await _dbContext.ExecuteAsync(
            MessageQueries.InsertMessage,
            request.ToSqlRequest(),
            cancellationToken);

        return rowsAffected > 0;
    }

    public Task<int> CountAsync(MessageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug("Attempting to count messages");

        var parameters = CreateQueryParameters(query);
        var sql = BuildMessagesPageSql(MessageQueries.CountMessagesPage, query, includeOrderAndPaging: false);

        return _dbContext.QueryFirstOrDefaultAsync<int>(sql, parameters, cancellationToken);
    }

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to delete messages older than {CutoffUtc}", cutoffUtc);

        return _dbContext.ExecuteAsync(
            MessageQueries.DeleteMessagesOlderThan,
            new { CutoffUtc = cutoffUtc.ToString("O") },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetPageAsync(
        MessageQuery query,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug(
            "Attempting to fetch messages page with offset {Offset} and take {Take}",
            offset,
            take);

        var parameters = CreateQueryParameters(query);
        parameters.Add("Offset", offset);
        parameters.Add("Take", take);

        var sqlResponses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            BuildMessagesPageSql(MessageQueries.SelectMessagesPage, query, includeOrderAndPaging: true),
            parameters,
            cancellationToken);

        return sqlResponses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch recent messages with take: {Take}", take);

        var sqlResponses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            MessageQueries.GetRecentMessages,
            new { Take = take },
            cancellationToken);

        return sqlResponses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentByBrokerAsync(
        string brokerServer,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch recent messages for broker {BrokerServer} with take: {Take}",
            brokerServer,
            take);

        var sqlResponses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            MessageQueries.GetRecentMessagesByBroker,
            new
            {
                BrokerServer = brokerServer,
                Take = take
            },
            cancellationToken);

        return sqlResponses.MapToMessages();
    }

    public async Task<ChannelSummary> GetChannelSummaryAsync(
        string region,
        string channel,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch channel summary for region {Region}, channel {Channel}",
            region,
            channel);

        var response = await _dbContext.QueryFirstOrDefaultAsync<ChannelSummarySqlResponse>(
            MessageQueries.GetChannelSummary,
            CreateChannelQueryParameters(region, channel),
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

    public async Task<IReadOnlyCollection<ChannelTopNode>> GetTopNodesByChannelAsync(
        string region,
        string channel,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch top nodes for region {Region}, channel {Channel} with take: {Take}",
            region,
            channel,
            take);

        var sqlResponses = await _dbContext.QueryAsync<ChannelTopNodeSqlResponse>(
            MessageQueries.GetTopNodesByChannel,
            CreateChannelQueryParameters(region, channel, take),
            cancellationToken);

        return sqlResponses
            .Select(response => new ChannelTopNode
            {
                NodeId = response.NodeId,
                DisplayName = response.DisplayName,
                PacketCount = response.PacketCount
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentByChannelAsync(
        string region,
        string channel,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch recent messages for region {Region}, channel {Channel} with take: {Take}",
            region,
            channel,
            take);

        var sqlResponses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            MessageQueries.GetRecentMessagesByChannel,
            new
            {
                EncryptedTopicPattern = CreateChannelTopicPattern(region, channel, "e"),
                JsonTopicPattern = CreateChannelTopicPattern(region, channel, "json"),
                Take = take
            },
            cancellationToken);

        return sqlResponses.MapToMessages();
    }

    public async Task<IReadOnlyCollection<MessageSummary>> GetRecentBySenderAsync(
        string senderNodeId,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Attempting to fetch recent messages by sender {SenderNodeId} with take: {Take}",
            senderNodeId,
            take);

        var sqlResponses = await _dbContext.QueryAsync<MessageSummarySqlResponse>(
            MessageQueries.GetRecentMessagesBySender,
            new
            {
                SenderNodeId = senderNodeId,
                Take = take
            },
            cancellationToken);

        return sqlResponses.MapToMessages();
    }

    private static string CreateChannelTopicPattern(string region, string channel, string transport)
    {
        return $"msh/{region}/%/{transport}/{channel}/%";
    }

    private static object CreateChannelQueryParameters(string region, string channel, int? take = null)
    {
        return new
        {
            EncryptedTopicPattern = CreateChannelTopicPattern(region, channel, "e"),
            JsonTopicPattern = CreateChannelTopicPattern(region, channel, "json"),
            Take = take
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
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
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

    private static string BuildMessagesPageSql(string baseSql, MessageQuery query, bool includeOrderAndPaging)
    {
        var sqlBuilder = new System.Text.StringBuilder(baseSql)
            .AppendLine()
            .AppendLine("WHERE 1 = 1");

        if (!string.IsNullOrWhiteSpace(query.BrokerServer))
        {
            sqlBuilder.AppendLine("  AND COALESCE(mh.broker_server, 'unknown') = @BrokerServer");
        }

        if (!string.IsNullOrWhiteSpace(query.PacketType))
        {
            sqlBuilder.AppendLine("  AND mh.packet_type = @PacketType");
        }

        switch (query.Visibility)
        {
            case MessageVisibilityFilter.DecodedOnly:
                sqlBuilder.AppendLine("  AND mh.packet_type NOT IN @OpaquePacketTypes");
                break;
            case MessageVisibilityFilter.OpaqueOnly:
                sqlBuilder.AppendLine("  AND mh.packet_type IN @OpaquePacketTypes");
                break;
            case MessageVisibilityFilter.PrivateOnly:
                sqlBuilder.AppendLine("  AND mh.is_private = 1");
                break;
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            sqlBuilder.AppendLine(
                """
                  AND (
                        mh.from_node_id LIKE @SearchPattern
                     OR COALESCE(n.short_name, '') LIKE @SearchPattern
                     OR COALESCE(n.long_name, '') LIKE @SearchPattern
                     OR COALESCE(mh.to_node_id, '') LIKE @SearchPattern
                     OR mh.packet_type LIKE @SearchPattern
                     OR mh.payload_preview LIKE @SearchPattern
                  )
                """);
        }

        if (includeOrderAndPaging)
        {
            sqlBuilder.AppendLine("ORDER BY mh.received_at_utc DESC, mh.id DESC");
            sqlBuilder.AppendLine("LIMIT @Take OFFSET @Offset;");
        }
        else
        {
            sqlBuilder.Append(';');
        }

        return sqlBuilder.ToString();
    }

    private static Dapper.DynamicParameters CreateQueryParameters(MessageQuery query)
    {
        var parameters = new Dapper.DynamicParameters();
        var normalizedBrokerServer = query.BrokerServer.Trim();
        var normalizedPacketType = query.PacketType.Trim();
        var normalizedSearchText = query.SearchText.Trim();

        parameters.Add("BrokerServer", normalizedBrokerServer);
        parameters.Add("PacketType", normalizedPacketType);
        parameters.Add("SearchPattern", $"%{normalizedSearchText}%");
        parameters.Add("OpaquePacketTypes", OpaquePacketTypes);

        return parameters;
    }
}
