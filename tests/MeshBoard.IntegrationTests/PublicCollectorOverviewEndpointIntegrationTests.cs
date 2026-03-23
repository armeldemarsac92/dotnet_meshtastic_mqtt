using System.Net;
using System.Net.Http.Json;
using MeshBoard.Contracts.Collector;
using Npgsql;

namespace MeshBoard.IntegrationTests;

public sealed class PublicCollectorOverviewEndpointIntegrationTests
{
    [Fact]
    public async Task PublicCollectorOverviewEndpoint_ShouldCountAllMatchingChannelsEvenWhenDetailsAreCapped()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();
        await SeedCollectorOverviewCapScenarioAsync(host.PersistenceConnectionString);

        var response = await client.GetAsync(
            "/api/public/collector/overview?serverAddress=mqtt.world.example:1883&activeWithinHours=48&lookbackHours=48&maxChannels=10&topPacketTypes=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await response.Content.ReadFromJsonAsync<CollectorOverviewSnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.ServerCount);
        Assert.Equal(12, snapshot.ChannelCount);

        var server = Assert.Single(snapshot.Servers);
        Assert.Equal(12, server.ChannelCount);
        Assert.Equal(10, server.Channels.Count);
    }

    [Fact]
    public async Task PublicCollectorOverviewEndpoint_ShouldReturnNestedServerAndChannelSummaries()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();
        await SeedCollectorOverviewAsync(host.PersistenceConnectionString);

        var response = await client.GetAsync(
            "/api/public/collector/overview?serverAddress=mqtt.world.example:1883&activeWithinHours=48&lookbackHours=48&maxChannels=10&topPacketTypes=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await response.Content.ReadFromJsonAsync<CollectorOverviewSnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.ServerCount);
        Assert.Equal(2, snapshot.ChannelCount);
        Assert.Equal(5, snapshot.ActiveNodeCount);
        Assert.Equal(2, snapshot.ActiveLinkCount);
        Assert.Equal(13, snapshot.PacketCountInLookback);
        Assert.Equal(9, snapshot.NeighborObservationCountInLookback);

        var server = Assert.Single(snapshot.Servers);
        Assert.Equal("mqtt.world.example:1883", server.ServerAddress);
        Assert.Equal(2, server.ChannelCount);
        Assert.Equal(5, server.ActiveNodeCount);
        Assert.Equal(2, server.ActiveLinkCount);
        Assert.Equal(13, server.PacketCountInLookback);
        Assert.Equal(9, server.NeighborObservationCountInLookback);

        var longFast = Assert.Single(server.Channels, channel => channel.Region == "US" && channel.ChannelName == "LongFast");
        Assert.Equal("2", longFast.MeshVersion);
        Assert.Equal(3, longFast.ActiveNodeCount);
        Assert.Equal(2, longFast.ActivePositionedNodeCount);
        Assert.Equal(2, longFast.ActiveLinkCount);
        Assert.Equal(1, longFast.ConnectedComponentCount);
        Assert.Equal(3, longFast.LargestConnectedComponentSize);
        Assert.Equal(0, longFast.IsolatedNodeCount);
        Assert.Equal(1, longFast.BridgeNodeCount);
        Assert.Equal(8, longFast.PacketCountInLookback);
        Assert.Equal(9, longFast.NeighborObservationCountInLookback);
        Assert.Collection(
            longFast.TopPacketTypes,
            packetType =>
            {
                Assert.Equal("Position Update", packetType.PacketType);
                Assert.Equal(5, packetType.PacketCount);
            },
            packetType =>
            {
                Assert.Equal("Telemetry", packetType.PacketType);
                Assert.Equal(2, packetType.PacketCount);
            });

        var longSlow = Assert.Single(server.Channels, channel => channel.Region == "EU" && channel.ChannelName == "LongSlow");
        Assert.Equal(2, longSlow.ActiveNodeCount);
        Assert.Equal(1, longSlow.ActivePositionedNodeCount);
        Assert.Equal(0, longSlow.ActiveLinkCount);
        Assert.Equal(2, longSlow.ConnectedComponentCount);
        Assert.Equal(1, longSlow.LargestConnectedComponentSize);
        Assert.Equal(2, longSlow.IsolatedNodeCount);
        Assert.Equal(0, longSlow.BridgeNodeCount);
        Assert.Equal(5, longSlow.PacketCountInLookback);
        Assert.Equal(0, longSlow.NeighborObservationCountInLookback);
        Assert.Collection(
            longSlow.TopPacketTypes,
            packetType =>
            {
                Assert.Equal("Text Message", packetType.PacketType);
                Assert.Equal(4, packetType.PacketCount);
            },
            packetType =>
            {
                Assert.Equal("Routing", packetType.PacketType);
                Assert.Equal(1, packetType.PacketCount);
            });
    }

    private static async Task SeedCollectorOverviewAsync(string connectionString)
    {
        var observedAtUtc = DateTimeOffset.Parse("2026-03-22T10:00:00Z");
        var bucketStartUtc = DateTimeOffset.Parse("2026-03-22T10:00:00Z");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var serverId = await InsertServerAsync(connection, observedAtUtc);
        var longFastChannelId = await InsertChannelAsync(connection, serverId, "US", "LongFast", observedAtUtc);
        var longSlowChannelId = await InsertChannelAsync(connection, serverId, "EU", "LongSlow", observedAtUtc.AddMinutes(-5));

        await InsertNodeAsync(connection, serverId, longFastChannelId, "!alpha", "ALP", "Alpha", 48.8566, 2.3522, observedAtUtc);
        await InsertNodeAsync(connection, serverId, longFastChannelId, "!bravo", "BRV", "Bravo", 45.7640, 4.8357, observedAtUtc.AddMinutes(-1));
        await InsertNodeAsync(connection, serverId, longFastChannelId, "!charlie", "CHR", "Charlie", null, null, observedAtUtc.AddMinutes(-2));

        await InsertNodeAsync(connection, serverId, longSlowChannelId, "!delta", "DLT", "Delta", 51.5074, -0.1278, observedAtUtc.AddMinutes(-3));
        await InsertNodeAsync(connection, serverId, longSlowChannelId, "!echo", "ECH", "Echo", null, null, observedAtUtc.AddMinutes(-4));

        await InsertCurrentLinkAsync(connection, longFastChannelId, "!alpha", "!bravo", 6.5f, observedAtUtc);
        await InsertCurrentLinkAsync(connection, longFastChannelId, "!bravo", "!charlie", 4.0f, observedAtUtc.AddMinutes(-1));

        await InsertChannelRollupAsync(connection, longFastChannelId, bucketStartUtc, "Position Update", 5, observedAtUtc, observedAtUtc.AddMinutes(20));
        await InsertChannelRollupAsync(connection, longFastChannelId, bucketStartUtc, "Telemetry", 2, observedAtUtc.AddMinutes(2), observedAtUtc.AddMinutes(10));
        await InsertChannelRollupAsync(connection, longFastChannelId, bucketStartUtc, "Neighbor Info", 1, observedAtUtc.AddMinutes(3), observedAtUtc.AddMinutes(3));
        await InsertChannelRollupAsync(connection, longSlowChannelId, bucketStartUtc, "Text Message", 4, observedAtUtc.AddMinutes(4), observedAtUtc.AddMinutes(15));
        await InsertChannelRollupAsync(connection, longSlowChannelId, bucketStartUtc, "Routing", 1, observedAtUtc.AddMinutes(5), observedAtUtc.AddMinutes(5));

        await InsertLinkRollupAsync(connection, longFastChannelId, bucketStartUtc, "!alpha", "!bravo", 6, 36.0, 7.0f, 6.5f, observedAtUtc, observedAtUtc.AddMinutes(10));
        await InsertLinkRollupAsync(connection, longFastChannelId, bucketStartUtc, "!bravo", "!charlie", 3, 12.0, 4.5f, 4.0f, observedAtUtc.AddMinutes(1), observedAtUtc.AddMinutes(11));
    }

    private static async Task SeedCollectorOverviewCapScenarioAsync(string connectionString)
    {
        var observedAtUtc = DateTimeOffset.Parse("2026-03-22T10:00:00Z");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var serverId = await InsertServerAsync(connection, observedAtUtc);

        for (var index = 0; index < 12; index++)
        {
            await InsertChannelAsync(
                connection,
                serverId,
                "US",
                $"Channel-{index + 1:D2}",
                observedAtUtc.AddMinutes(-index));
        }
    }

    private static async Task<long> InsertServerAsync(NpgsqlConnection connection, DateTimeOffset observedAtUtc)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO collector_servers (
                server_address,
                first_observed_at_utc,
                last_observed_at_utc)
            VALUES (
                'mqtt.world.example:1883',
                @ObservedAtUtc,
                @ObservedAtUtc)
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing server id."));
    }

    private static async Task<long> InsertChannelAsync(
        NpgsqlConnection connection,
        long serverId,
        string region,
        string channelName,
        DateTimeOffset observedAtUtc)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO collector_channels (
                server_id,
                region,
                mesh_version,
                channel_name,
                topic_pattern,
                first_observed_at_utc,
                last_observed_at_utc)
            VALUES (
                @ServerId,
                @Region,
                '2',
                @ChannelName,
                @TopicPattern,
                @ObservedAtUtc,
                @ObservedAtUtc)
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("ServerId", serverId);
        command.Parameters.AddWithValue("Region", region);
        command.Parameters.AddWithValue("ChannelName", channelName);
        command.Parameters.AddWithValue("TopicPattern", $"msh/{region}/2/e/{channelName}/#");
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing channel id."));
    }

    private static async Task InsertNodeAsync(
        NpgsqlConnection connection,
        long serverId,
        long channelId,
        string nodeId,
        string shortName,
        string longName,
        double? latitude,
        double? longitude,
        DateTimeOffset observedAtUtc)
    {
        await using var command = new NpgsqlCommand(
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
                last_known_longitude)
            VALUES (
                @ServerId,
                @NodeId,
                @ShortName,
                @LongName,
                @ChannelId,
                @ObservedAtUtc,
                @ObservedAtUtc,
                @Latitude,
                @Longitude);
            """,
            connection);
        command.Parameters.AddWithValue("ServerId", serverId);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("NodeId", nodeId);
        command.Parameters.AddWithValue("ShortName", shortName);
        command.Parameters.AddWithValue("LongName", longName);
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        command.Parameters.AddWithValue("Latitude", latitude is null ? DBNull.Value : latitude.Value);
        command.Parameters.AddWithValue("Longitude", longitude is null ? DBNull.Value : longitude.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertCurrentLinkAsync(
        NpgsqlConnection connection,
        long channelId,
        string sourceNodeId,
        string targetNodeId,
        float snrDb,
        DateTimeOffset lastSeenAtUtc)
    {
        await using var command = new NpgsqlCommand(
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
                @LastSeenAtUtc);
            """,
            connection);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("SourceNodeId", sourceNodeId);
        command.Parameters.AddWithValue("TargetNodeId", targetNodeId);
        command.Parameters.AddWithValue("SnrDb", snrDb);
        command.Parameters.AddWithValue("LastSeenAtUtc", lastSeenAtUtc);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertChannelRollupAsync(
        NpgsqlConnection connection,
        long channelId,
        DateTimeOffset bucketStartUtc,
        string packetType,
        int packetCount,
        DateTimeOffset firstSeenAtUtc,
        DateTimeOffset lastSeenAtUtc)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO collector_channel_packet_hourly_rollups (
                channel_id,
                bucket_start_utc,
                packet_type,
                packet_count,
                first_seen_at_utc,
                last_seen_at_utc)
            VALUES (
                @ChannelId,
                @BucketStartUtc,
                @PacketType,
                @PacketCount,
                @FirstSeenAtUtc,
                @LastSeenAtUtc);
            """,
            connection);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("BucketStartUtc", bucketStartUtc);
        command.Parameters.AddWithValue("PacketType", packetType);
        command.Parameters.AddWithValue("PacketCount", packetCount);
        command.Parameters.AddWithValue("FirstSeenAtUtc", firstSeenAtUtc);
        command.Parameters.AddWithValue("LastSeenAtUtc", lastSeenAtUtc);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertLinkRollupAsync(
        NpgsqlConnection connection,
        long channelId,
        DateTimeOffset bucketStartUtc,
        string sourceNodeId,
        string targetNodeId,
        int observationCount,
        double snrSumDb,
        float maxSnrDb,
        float lastSnrDb,
        DateTimeOffset firstSeenAtUtc,
        DateTimeOffset lastSeenAtUtc)
    {
        await using var command = new NpgsqlCommand(
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
                @ObservationCount,
                @ObservationCount,
                @SnrSumDb,
                @MaxSnrDb,
                @LastSnrDb,
                @FirstSeenAtUtc,
                @LastSeenAtUtc);
            """,
            connection);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("BucketStartUtc", bucketStartUtc);
        command.Parameters.AddWithValue("SourceNodeId", sourceNodeId);
        command.Parameters.AddWithValue("TargetNodeId", targetNodeId);
        command.Parameters.AddWithValue("ObservationCount", observationCount);
        command.Parameters.AddWithValue("SnrSumDb", snrSumDb);
        command.Parameters.AddWithValue("MaxSnrDb", maxSnrDb);
        command.Parameters.AddWithValue("LastSnrDb", lastSnrDb);
        command.Parameters.AddWithValue("FirstSeenAtUtc", firstSeenAtUtc);
        command.Parameters.AddWithValue("LastSeenAtUtc", lastSeenAtUtc);
        await command.ExecuteNonQueryAsync();
    }
}
