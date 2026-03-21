using System.Globalization;
using Dapper;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.Mapping;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class CollectorReadRepository : ICollectorReadRepository
{
    private const string FilteredChannelsCte =
        """
        WITH filtered_channels AS (
            SELECT
                c.id AS ChannelId,
                s.server_address AS ServerAddress,
                c.region AS Region,
                c.mesh_version AS MeshVersion,
                c.channel_name AS ChannelName,
                c.topic_pattern AS TopicPattern,
                c.first_observed_at_utc::text AS FirstObservedAtUtc,
                c.last_observed_at_utc::text AS LastObservedAtUtc
            FROM collector_channels c
            INNER JOIN collector_servers s
                ON s.id = c.server_id
            WHERE c.workspace_id = @WorkspaceId
              AND (@ServerAddress = '' OR s.server_address = @ServerAddress)
              AND (@Region = '' OR c.region = @Region)
              AND (@ChannelName = '' OR c.channel_name = @ChannelName)
        )
        """;

    private const string LatestMapNodesCte =
        FilteredChannelsCte +
        """
        ,
        latest_nodes AS (
            SELECT DISTINCT ON (n.node_id)
                n.node_id AS NodeId,
                fc.ServerAddress AS BrokerServer,
                n.short_name AS ShortName,
                n.long_name AS LongName,
                n.last_heard_at_utc::text AS LastHeardAtUtc,
                CONCAT(fc.Region, '/', fc.ChannelName) AS LastHeardChannel,
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
            INNER JOIN filtered_channels fc
                ON fc.ChannelId = n.channel_id
            WHERE n.workspace_id = @WorkspaceId
              AND n.last_known_latitude IS NOT NULL
              AND n.last_known_longitude IS NOT NULL
              AND COALESCE(n.last_heard_at_utc, n.last_text_message_at_utc) >= @NotBeforeUtc
            ORDER BY n.node_id,
                     COALESCE(n.last_heard_at_utc, n.last_text_message_at_utc) DESC NULLS LAST,
                     n.id DESC
        )
        """;

    private readonly IDbContext _dbContext;

    public CollectorReadRepository(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<CollectorServerSummary>> GetServersAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<CollectorServerSummarySqlResponse>(
            """
            SELECT
                s.server_address AS ServerAddress,
                s.first_observed_at_utc::text AS FirstObservedAtUtc,
                s.last_observed_at_utc::text AS LastObservedAtUtc,
                (
                    SELECT COUNT(1)
                    FROM collector_channels c
                    WHERE c.workspace_id = s.workspace_id
                      AND c.server_id = s.id
                ) AS ChannelCount,
                (
                    SELECT COUNT(DISTINCT n.node_id)
                    FROM collector_nodes n
                    INNER JOIN collector_channels c
                        ON c.id = n.channel_id
                    WHERE n.workspace_id = s.workspace_id
                      AND c.server_id = s.id
                ) AS NodeCount,
                (
                    SELECT COUNT(1)
                    FROM collector_messages m
                    INNER JOIN collector_channels c
                        ON c.id = m.channel_id
                    WHERE m.workspace_id = s.workspace_id
                      AND c.server_id = s.id
                ) AS MessageCount,
                (
                    SELECT COUNT(1)
                    FROM collector_neighbor_links l
                    INNER JOIN collector_channels c
                        ON c.id = l.channel_id
                    WHERE l.workspace_id = s.workspace_id
                      AND c.server_id = s.id
                ) AS NeighborLinkCount
            FROM collector_servers s
            WHERE s.workspace_id = @WorkspaceId
            ORDER BY s.last_observed_at_utc DESC,
                     s.server_address ASC;
            """,
            new { WorkspaceId = workspaceId.Trim() },
            cancellationToken);

        return responses.Select(MapServer).ToArray();
    }

    public async Task<IReadOnlyCollection<CollectorChannelSummary>> GetChannelsAsync(
        string workspaceId,
        CollectorMapQuery query,
        CancellationToken cancellationToken = default)
    {
        var responses = await _dbContext.QueryAsync<CollectorChannelSummarySqlResponse>(
            FilteredChannelsCte +
            """
            SELECT
                fc.ServerAddress,
                fc.Region,
                fc.MeshVersion,
                fc.ChannelName,
                fc.TopicPattern,
                fc.FirstObservedAtUtc,
                fc.LastObservedAtUtc,
                (
                    SELECT COUNT(DISTINCT n.node_id)
                    FROM collector_nodes n
                    WHERE n.workspace_id = @WorkspaceId
                      AND n.channel_id = fc.ChannelId
                ) AS NodeCount,
                (
                    SELECT COUNT(1)
                    FROM collector_messages m
                    WHERE m.workspace_id = @WorkspaceId
                      AND m.channel_id = fc.ChannelId
                ) AS MessageCount,
                (
                    SELECT COUNT(1)
                    FROM collector_neighbor_links l
                    WHERE l.workspace_id = @WorkspaceId
                      AND l.channel_id = fc.ChannelId
                ) AS NeighborLinkCount
            FROM filtered_channels fc
            ORDER BY fc.ServerAddress ASC,
                     fc.Region ASC,
                     fc.ChannelName ASC;
            """,
            CreateFilterParameters(workspaceId, query),
            cancellationToken);

        return responses.Select(MapChannel).ToArray();
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetMapNodesAsync(
        string workspaceId,
        CollectorMapQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var parameters = CreateFilterParameters(workspaceId, query);
        parameters.Add("NotBeforeUtc", notBeforeUtc);
        parameters.Add("MaxNodes", query.MaxNodes);

        var responses = await _dbContext.QueryAsync<NodeSqlResponse>(
            LatestMapNodesCte +
            """
            SELECT *
            FROM latest_nodes
            ORDER BY COALESCE(LastHeardAtUtc, LastTextMessageAtUtc, '') DESC,
                     COALESCE(LongName, ShortName, NodeId) ASC,
                     NodeId ASC
            LIMIT @MaxNodes;
            """,
            parameters,
            cancellationToken);

        return responses.MapToNodes();
    }

    public async Task<IReadOnlyCollection<CollectorMapLinkSummary>> GetMapLinksAsync(
        string workspaceId,
        CollectorMapQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var parameters = CreateFilterParameters(workspaceId, query);
        parameters.Add("NotBeforeUtc", notBeforeUtc);
        parameters.Add("MaxLinks", query.MaxLinks);

        var responses = await _dbContext.QueryAsync<CollectorMapLinkSqlResponse>(
            LatestMapNodesCte +
            """
            SELECT
                l.source_node_id AS SourceNodeId,
                l.target_node_id AS TargetNodeId,
                l.snr_db AS SnrDb,
                l.last_seen_at_utc::text AS LastSeenAtUtc,
                fc.ServerAddress,
                fc.Region,
                fc.MeshVersion,
                fc.ChannelName
            FROM collector_neighbor_links l
            INNER JOIN filtered_channels fc
                ON fc.ChannelId = l.channel_id
            INNER JOIN latest_nodes source_node
                ON source_node.NodeId = l.source_node_id
            INNER JOIN latest_nodes target_node
                ON target_node.NodeId = l.target_node_id
            WHERE l.workspace_id = @WorkspaceId
              AND l.last_seen_at_utc >= @NotBeforeUtc
            ORDER BY l.last_seen_at_utc DESC,
                     l.source_node_id ASC,
                     l.target_node_id ASC
            LIMIT @MaxLinks;
            """,
            parameters,
            cancellationToken);

        return responses.Select(MapLink).ToArray();
    }

    public async Task<IReadOnlyCollection<CollectorChannelPacketHourlyRollup>> GetChannelPacketRollupsAsync(
        string workspaceId,
        CollectorPacketStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var parameters = CreatePacketStatsParameters(workspaceId, query);
        parameters.Add("NotBeforeUtc", notBeforeUtc);
        parameters.Add("MaxRows", query.MaxRows);

        var responses = await _dbContext.QueryAsync<CollectorChannelPacketHourlyRollupSqlResponse>(
            FilteredChannelsCte +
            """
            ,
            active_nodes AS (
                SELECT
                    workspace_id,
                    channel_id,
                    bucket_start_utc,
                    packet_type,
                    COUNT(1) AS ActiveNodeCount
                FROM collector_node_packet_hourly_rollups
                WHERE workspace_id = @WorkspaceId
                  AND bucket_start_utc >= @NotBeforeUtc
                  AND (@PacketType = '' OR packet_type = @PacketType)
                GROUP BY workspace_id, channel_id, bucket_start_utc, packet_type
            )
            SELECT
                r.bucket_start_utc::text AS BucketStartUtc,
                fc.ServerAddress,
                fc.Region,
                fc.MeshVersion,
                fc.ChannelName,
                r.packet_type AS PacketType,
                r.packet_count AS PacketCount,
                COALESCE(active_nodes.ActiveNodeCount, 0) AS ActiveNodeCount,
                r.first_seen_at_utc::text AS FirstSeenAtUtc,
                r.last_seen_at_utc::text AS LastSeenAtUtc
            FROM collector_channel_packet_hourly_rollups r
            INNER JOIN filtered_channels fc
                ON fc.ChannelId = r.channel_id
            LEFT JOIN active_nodes
                ON active_nodes.workspace_id = r.workspace_id
               AND active_nodes.channel_id = r.channel_id
               AND active_nodes.bucket_start_utc = r.bucket_start_utc
               AND active_nodes.packet_type = r.packet_type
            WHERE r.workspace_id = @WorkspaceId
              AND r.bucket_start_utc >= @NotBeforeUtc
              AND (@PacketType = '' OR r.packet_type = @PacketType)
            ORDER BY r.bucket_start_utc DESC,
                     r.packet_count DESC,
                     fc.ServerAddress ASC,
                     fc.Region ASC,
                     fc.ChannelName ASC,
                     r.packet_type ASC
            LIMIT @MaxRows;
            """,
            parameters,
            cancellationToken);

        return responses.Select(MapChannelPacketRollup).ToArray();
    }

    public async Task<IReadOnlyCollection<CollectorNodePacketHourlyRollup>> GetNodePacketRollupsAsync(
        string workspaceId,
        CollectorPacketStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var parameters = CreatePacketStatsParameters(workspaceId, query);
        parameters.Add("NotBeforeUtc", notBeforeUtc);
        parameters.Add("MaxRows", query.MaxRows);

        var responses = await _dbContext.QueryAsync<CollectorNodePacketHourlyRollupSqlResponse>(
            FilteredChannelsCte +
            """
            SELECT
                r.bucket_start_utc::text AS BucketStartUtc,
                fc.ServerAddress,
                fc.Region,
                fc.MeshVersion,
                fc.ChannelName,
                r.node_id AS NodeId,
                n.short_name AS ShortName,
                n.long_name AS LongName,
                r.packet_type AS PacketType,
                r.packet_count AS PacketCount,
                r.first_seen_at_utc::text AS FirstSeenAtUtc,
                r.last_seen_at_utc::text AS LastSeenAtUtc
            FROM collector_node_packet_hourly_rollups r
            INNER JOIN filtered_channels fc
                ON fc.ChannelId = r.channel_id
            LEFT JOIN collector_nodes n
                ON n.workspace_id = r.workspace_id
               AND n.channel_id = r.channel_id
               AND n.node_id = r.node_id
            WHERE r.workspace_id = @WorkspaceId
              AND r.bucket_start_utc >= @NotBeforeUtc
              AND (@NodeId = '' OR r.node_id = @NodeId)
              AND (@PacketType = '' OR r.packet_type = @PacketType)
            ORDER BY r.bucket_start_utc DESC,
                     r.packet_count DESC,
                     r.node_id ASC,
                     r.packet_type ASC
            LIMIT @MaxRows;
            """,
            parameters,
            cancellationToken);

        return responses.Select(MapNodePacketRollup).ToArray();
    }

    public async Task<IReadOnlyCollection<CollectorNeighborLinkHourlyRollup>> GetNeighborLinkRollupsAsync(
        string workspaceId,
        CollectorNeighborLinkStatsQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var parameters = CreateNeighborLinkStatsParameters(workspaceId, query);
        parameters.Add("NotBeforeUtc", notBeforeUtc);
        parameters.Add("MaxRows", query.MaxRows);

        var responses = await _dbContext.QueryAsync<CollectorNeighborLinkHourlyRollupSqlResponse>(
            FilteredChannelsCte +
            """
            SELECT
                r.bucket_start_utc::text AS BucketStartUtc,
                fc.ServerAddress,
                fc.Region,
                fc.MeshVersion,
                fc.ChannelName,
                r.source_node_id AS SourceNodeId,
                r.target_node_id AS TargetNodeId,
                source_node.short_name AS SourceShortName,
                source_node.long_name AS SourceLongName,
                target_node.short_name AS TargetShortName,
                target_node.long_name AS TargetLongName,
                r.observation_count AS ObservationCount,
                CASE
                    WHEN r.snr_sample_count > 0 THEN (r.snr_sum_db / r.snr_sample_count)::real
                    ELSE NULL
                END AS AverageSnrDb,
                r.max_snr_db AS MaxSnrDb,
                r.last_snr_db AS LastSnrDb,
                r.first_seen_at_utc::text AS FirstSeenAtUtc,
                r.last_seen_at_utc::text AS LastSeenAtUtc
            FROM collector_neighbor_link_hourly_rollups r
            INNER JOIN filtered_channels fc
                ON fc.ChannelId = r.channel_id
            LEFT JOIN collector_nodes source_node
                ON source_node.workspace_id = r.workspace_id
               AND source_node.channel_id = r.channel_id
               AND source_node.node_id = r.source_node_id
            LEFT JOIN collector_nodes target_node
                ON target_node.workspace_id = r.workspace_id
               AND target_node.channel_id = r.channel_id
               AND target_node.node_id = r.target_node_id
            WHERE r.workspace_id = @WorkspaceId
              AND r.bucket_start_utc >= @NotBeforeUtc
              AND (@SourceNodeId = '' OR r.source_node_id = @SourceNodeId)
              AND (@TargetNodeId = '' OR r.target_node_id = @TargetNodeId)
            ORDER BY r.bucket_start_utc DESC,
                     r.observation_count DESC,
                     r.source_node_id ASC,
                     r.target_node_id ASC
            LIMIT @MaxRows;
            """,
            parameters,
            cancellationToken);

        return responses.Select(MapNeighborLinkRollup).ToArray();
    }

    private static DynamicParameters CreateFilterParameters(string workspaceId, CollectorMapQuery query)
    {
        var parameters = new DynamicParameters();
        parameters.Add("WorkspaceId", workspaceId.Trim());
        parameters.Add("ServerAddress", query.ServerAddress ?? string.Empty);
        parameters.Add("Region", query.Region ?? string.Empty);
        parameters.Add("ChannelName", query.ChannelName ?? string.Empty);
        return parameters;
    }

    private static DynamicParameters CreatePacketStatsParameters(string workspaceId, CollectorPacketStatsQuery query)
    {
        var parameters = new DynamicParameters();
        parameters.Add("WorkspaceId", workspaceId.Trim());
        parameters.Add("ServerAddress", query.ServerAddress ?? string.Empty);
        parameters.Add("Region", query.Region ?? string.Empty);
        parameters.Add("ChannelName", query.ChannelName ?? string.Empty);
        parameters.Add("NodeId", query.NodeId ?? string.Empty);
        parameters.Add("PacketType", query.PacketType ?? string.Empty);
        return parameters;
    }

    private static DynamicParameters CreateNeighborLinkStatsParameters(string workspaceId, CollectorNeighborLinkStatsQuery query)
    {
        var parameters = new DynamicParameters();
        parameters.Add("WorkspaceId", workspaceId.Trim());
        parameters.Add("ServerAddress", query.ServerAddress ?? string.Empty);
        parameters.Add("Region", query.Region ?? string.Empty);
        parameters.Add("ChannelName", query.ChannelName ?? string.Empty);
        parameters.Add("SourceNodeId", query.SourceNodeId ?? string.Empty);
        parameters.Add("TargetNodeId", query.TargetNodeId ?? string.Empty);
        return parameters;
    }

    private static CollectorServerSummary MapServer(CollectorServerSummarySqlResponse response)
    {
        return new CollectorServerSummary
        {
            ServerAddress = response.ServerAddress,
            FirstObservedAtUtc = ParseTimestamp(response.FirstObservedAtUtc),
            LastObservedAtUtc = ParseTimestamp(response.LastObservedAtUtc),
            ChannelCount = response.ChannelCount,
            NodeCount = response.NodeCount,
            MessageCount = response.MessageCount,
            NeighborLinkCount = response.NeighborLinkCount
        };
    }

    private static CollectorChannelSummary MapChannel(CollectorChannelSummarySqlResponse response)
    {
        return new CollectorChannelSummary
        {
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            TopicPattern = response.TopicPattern,
            FirstObservedAtUtc = ParseTimestamp(response.FirstObservedAtUtc),
            LastObservedAtUtc = ParseTimestamp(response.LastObservedAtUtc),
            NodeCount = response.NodeCount,
            MessageCount = response.MessageCount,
            NeighborLinkCount = response.NeighborLinkCount
        };
    }

    private static CollectorMapLinkSummary MapLink(CollectorMapLinkSqlResponse response)
    {
        return new CollectorMapLinkSummary
        {
            SourceNodeId = response.SourceNodeId,
            TargetNodeId = response.TargetNodeId,
            SnrDb = response.SnrDb,
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName
        };
    }

    private static CollectorChannelPacketHourlyRollup MapChannelPacketRollup(CollectorChannelPacketHourlyRollupSqlResponse response)
    {
        return new CollectorChannelPacketHourlyRollup
        {
            BucketStartUtc = ParseTimestamp(response.BucketStartUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            PacketType = response.PacketType,
            PacketCount = response.PacketCount,
            ActiveNodeCount = response.ActiveNodeCount,
            FirstSeenAtUtc = ParseTimestamp(response.FirstSeenAtUtc),
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc)
        };
    }

    private static CollectorNodePacketHourlyRollup MapNodePacketRollup(CollectorNodePacketHourlyRollupSqlResponse response)
    {
        return new CollectorNodePacketHourlyRollup
        {
            BucketStartUtc = ParseTimestamp(response.BucketStartUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            NodeId = response.NodeId,
            ShortName = response.ShortName,
            LongName = response.LongName,
            PacketType = response.PacketType,
            PacketCount = response.PacketCount,
            FirstSeenAtUtc = ParseTimestamp(response.FirstSeenAtUtc),
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc)
        };
    }

    private static CollectorNeighborLinkHourlyRollup MapNeighborLinkRollup(CollectorNeighborLinkHourlyRollupSqlResponse response)
    {
        return new CollectorNeighborLinkHourlyRollup
        {
            BucketStartUtc = ParseTimestamp(response.BucketStartUtc),
            ServerAddress = response.ServerAddress,
            Region = response.Region,
            MeshVersion = response.MeshVersion,
            ChannelName = response.ChannelName,
            SourceNodeId = response.SourceNodeId,
            TargetNodeId = response.TargetNodeId,
            SourceShortName = response.SourceShortName,
            SourceLongName = response.SourceLongName,
            TargetShortName = response.TargetShortName,
            TargetLongName = response.TargetLongName,
            ObservationCount = response.ObservationCount,
            AverageSnrDb = response.AverageSnrDb,
            MaxSnrDb = response.MaxSnrDb,
            LastSnrDb = response.LastSnrDb,
            FirstSeenAtUtc = ParseTimestamp(response.FirstSeenAtUtc),
            LastSeenAtUtc = ParseTimestamp(response.LastSeenAtUtc)
        };
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
