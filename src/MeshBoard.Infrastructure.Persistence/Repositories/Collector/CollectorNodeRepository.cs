using System.Text;
using Dapper;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Infrastructure.Persistence.Repositories.Collector;

internal sealed class CollectorNodeRepository : INodeRepository
{
    private const string LatestNodesCte =
        """
        WITH latest_nodes AS (
            SELECT
                n.node_id AS NodeId,
                s.server_address AS BrokerServer,
                n.short_name AS ShortName,
                n.long_name AS LongName,
                n.last_heard_at_utc::text AS LastHeardAtUtc,
                CASE
                    WHEN c.id IS NULL THEN NULL
                    ELSE CONCAT(c.region, '/', c.channel_name)
                END AS LastHeardChannel,
                n.last_text_message_at_utc::text AS LastTextMessageAtUtc,
                n.last_known_latitude AS LastKnownLatitude,
                n.last_known_longitude AS LastKnownLongitude,
                n.battery_level_percent AS BatteryLevelPercent,
                n.voltage AS Voltage,
                n.channel_utilization AS ChannelUtilization,
                n.air_util_tx AS AirUtilTx,
                n.uptime_seconds AS UptimeSeconds,
                n.temperature_celsius AS TemperatureCelsius,
                n.relative_humidity AS RelativeHumidity,
                n.barometric_pressure AS BarometricPressure
            FROM collector_nodes n
            INNER JOIN collector_servers s
                ON s.id = n.server_id
            LEFT JOIN collector_channels c
                ON c.id = n.last_heard_channel_id
            WHERE NOT @OnlyFavorites
              AND (
                  @SearchText = '' OR
                  n.node_id ILIKE @SearchPattern OR
                  COALESCE(n.short_name, '') ILIKE @SearchPattern OR
                  COALESCE(n.long_name, '') ILIKE @SearchPattern OR
                  COALESCE(CONCAT(c.region, '/', c.channel_name), '') ILIKE @SearchPattern
              )
              AND (
                  NOT @OnlyWithLocation OR
                  (n.last_known_latitude IS NOT NULL AND n.last_known_longitude IS NOT NULL)
              )
              AND (
                  NOT @OnlyWithTelemetry OR
                  n.battery_level_percent IS NOT NULL OR
                  n.voltage IS NOT NULL OR
                  n.channel_utilization IS NOT NULL OR
                  n.air_util_tx IS NOT NULL OR
                  n.uptime_seconds IS NOT NULL OR
                  n.temperature_celsius IS NOT NULL OR
                  n.relative_humidity IS NOT NULL OR
                  n.barometric_pressure IS NOT NULL
              )
        )
        """;

    private readonly CollectorChannelResolver _channelResolver;
    private readonly IDbContext _dbContext;
    private readonly ILogger<CollectorNodeRepository> _logger;

    public CollectorNodeRepository(
        IDbContext dbContext,
        CollectorChannelResolver channelResolver,
        ILogger<CollectorNodeRepository> logger)
    {
        _dbContext = dbContext;
        _channelResolver = channelResolver;
        _logger = logger;
    }

    public Task<int> CountAsync(NodeQuery query, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to count collector nodes");

        return _dbContext.QueryFirstOrDefaultAsync<int>(
            $"{LatestNodesCte}{Environment.NewLine}SELECT COUNT(1) FROM latest_nodes;",
            CreateQueryParameters(query),
            cancellationToken);
    }

    public async Task<NodeSummary?> GetByIdAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        _logger.LogDebug("Attempting to fetch collector node by id {NodeId}", nodeId);

        var responses = await _dbContext.QueryAsync<NodeSqlResponse>(
            """
            SELECT
                n.node_id AS NodeId,
                s.server_address AS BrokerServer,
                n.short_name AS ShortName,
                n.long_name AS LongName,
                n.last_heard_at_utc::text AS LastHeardAtUtc,
                CONCAT(c.region, '/', c.channel_name) AS LastHeardChannel,
                n.last_text_message_at_utc::text AS LastTextMessageAtUtc,
                n.last_known_latitude AS LastKnownLatitude,
                n.last_known_longitude AS LastKnownLongitude,
                n.battery_level_percent AS BatteryLevelPercent,
                n.voltage AS Voltage,
                n.channel_utilization AS ChannelUtilization,
                n.air_util_tx AS AirUtilTx,
                n.uptime_seconds AS UptimeSeconds,
                n.temperature_celsius AS TemperatureCelsius,
                n.relative_humidity AS RelativeHumidity,
                n.barometric_pressure AS BarometricPressure
            FROM collector_nodes n
            INNER JOIN collector_servers s
                ON s.id = n.server_id
            LEFT JOIN collector_channels c
                ON c.id = n.last_heard_channel_id
            WHERE n.node_id = @NodeId
            ORDER BY COALESCE(n.last_heard_at_utc, n.last_text_message_at_utc) DESC NULLS LAST,
                     n.id DESC
            LIMIT 1;
            """,
            new { NodeId = nodeId.Trim() },
            cancellationToken);

        return responses.MapToNodes().FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetLocatedAsync(
        string? searchText,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch collector located nodes with take {Take}", take);

        var query = new NodeQuery
        {
            SearchText = searchText ?? string.Empty,
            OnlyWithLocation = true
        };
        var parameters = CreateQueryParameters(query);
        parameters.Add("Take", take);

        var responses = await _dbContext.QueryAsync<NodeSqlResponse>(
            $"{LatestNodesCte}{Environment.NewLine}" +
            """
            SELECT *
            FROM latest_nodes
            ORDER BY COALESCE(LastHeardAtUtc, LastTextMessageAtUtc, '') DESC,
                     COALESCE(LongName, ShortName, NodeId) ASC,
                     NodeId ASC
            LIMIT @Take;
            """,
            parameters,
            cancellationToken);

        return responses.MapToNodes();
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetPageAsync(
        NodeQuery query,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch collector nodes with offset {Offset} and take {Take}", offset, take);

        var sqlBuilder = new StringBuilder(LatestNodesCte)
            .AppendLine()
            .AppendLine("SELECT *")
            .AppendLine("FROM latest_nodes")
            .AppendLine(GetOrderByClause(query.SortBy))
            .AppendLine("LIMIT @Take OFFSET @Offset;");

        var parameters = CreateQueryParameters(query);
        parameters.Add("Take", take);
        parameters.Add("Offset", offset);

        var responses = await _dbContext.QueryAsync<NodeSqlResponse>(
            sqlBuilder.ToString(),
            parameters,
            cancellationToken);

        return responses.MapToNodes();
    }

    public async Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to upsert collector observed node: {NodeId}", request.NodeId);

        var observedAtUtc = request.LastHeardAtUtc
            ?? request.LastTextMessageAtUtc
            ?? DateTimeOffset.UtcNow;
        var resolvedChannel = await _channelResolver.ResolveFromChannelKeyAsync(
            request.BrokerServer,
            request.LastHeardChannel,
            observedAtUtc,
            cancellationToken);

        await _dbContext.ExecuteAsync(
            """
            INSERT INTO collector_nodes (
                server_id,
                node_id,
                short_name,
                long_name,
                last_heard_channel_id,
                last_heard_at_utc,
                last_text_message_at_utc,
                last_known_latitude,
                last_known_longitude,
                battery_level_percent,
                voltage,
                channel_utilization,
                air_util_tx,
                uptime_seconds,
                temperature_celsius,
                relative_humidity,
                barometric_pressure)
            VALUES (
                @ServerId,
                @NodeId,
                @ShortName,
                @LongName,
                @LastHeardChannelId,
                @LastHeardAtUtc,
                @LastTextMessageAtUtc,
                @LastKnownLatitude,
                @LastKnownLongitude,
                @BatteryLevelPercent,
                @Voltage,
                @ChannelUtilization,
                @AirUtilTx,
                @UptimeSeconds,
                @TemperatureCelsius,
                @RelativeHumidity,
                @BarometricPressure)
            ON CONFLICT(server_id, node_id) DO UPDATE SET
                short_name = COALESCE(EXCLUDED.short_name, collector_nodes.short_name),
                long_name = COALESCE(EXCLUDED.long_name, collector_nodes.long_name),
                last_heard_channel_id = COALESCE(EXCLUDED.last_heard_channel_id, collector_nodes.last_heard_channel_id),
                last_heard_at_utc = COALESCE(EXCLUDED.last_heard_at_utc, collector_nodes.last_heard_at_utc),
                last_text_message_at_utc = COALESCE(EXCLUDED.last_text_message_at_utc, collector_nodes.last_text_message_at_utc),
                last_known_latitude = COALESCE(EXCLUDED.last_known_latitude, collector_nodes.last_known_latitude),
                last_known_longitude = COALESCE(EXCLUDED.last_known_longitude, collector_nodes.last_known_longitude),
                battery_level_percent = COALESCE(EXCLUDED.battery_level_percent, collector_nodes.battery_level_percent),
                voltage = COALESCE(EXCLUDED.voltage, collector_nodes.voltage),
                channel_utilization = COALESCE(EXCLUDED.channel_utilization, collector_nodes.channel_utilization),
                air_util_tx = COALESCE(EXCLUDED.air_util_tx, collector_nodes.air_util_tx),
                uptime_seconds = COALESCE(EXCLUDED.uptime_seconds, collector_nodes.uptime_seconds),
                temperature_celsius = COALESCE(EXCLUDED.temperature_celsius, collector_nodes.temperature_celsius),
                relative_humidity = COALESCE(EXCLUDED.relative_humidity, collector_nodes.relative_humidity),
                barometric_pressure = COALESCE(EXCLUDED.barometric_pressure, collector_nodes.barometric_pressure);
            """,
            new
            {
                ServerId = resolvedChannel.ServerId,
                LastHeardChannelId = resolvedChannel.ChannelId,
                request.NodeId,
                request.ShortName,
                request.LongName,
                request.LastHeardAtUtc,
                request.LastTextMessageAtUtc,
                request.LastKnownLatitude,
                request.LastKnownLongitude,
                request.BatteryLevelPercent,
                request.Voltage,
                request.ChannelUtilization,
                request.AirUtilTx,
                request.UptimeSeconds,
                request.TemperatureCelsius,
                request.RelativeHumidity,
                request.BarometricPressure
            },
            cancellationToken);
    }

    private static DynamicParameters CreateQueryParameters(NodeQuery query)
    {
        var parameters = new DynamicParameters();
        var normalizedSearchText = query.SearchText.Trim();

        parameters.Add("SearchText", normalizedSearchText);
        parameters.Add("SearchPattern", $"%{normalizedSearchText}%");
        parameters.Add("OnlyFavorites", query.OnlyFavorites);
        parameters.Add("OnlyWithLocation", query.OnlyWithLocation);
        parameters.Add("OnlyWithTelemetry", query.OnlyWithTelemetry);
        return parameters;
    }

    private static string GetOrderByClause(NodeSortOption sortBy)
    {
        return sortBy switch
        {
            NodeSortOption.NameAsc =>
                """
                ORDER BY COALESCE(LongName, ShortName, NodeId) ASC,
                         NodeId ASC
                """,
            NodeSortOption.BatteryDesc =>
                """
                ORDER BY COALESCE(BatteryLevelPercent, -1) DESC,
                         COALESCE(LongName, ShortName, NodeId) ASC
                """,
            _ =>
                """
                ORDER BY COALESCE(LastHeardAtUtc, LastTextMessageAtUtc, '') DESC,
                         COALESCE(LongName, ShortName, NodeId) ASC
                """
        };
    }

}
