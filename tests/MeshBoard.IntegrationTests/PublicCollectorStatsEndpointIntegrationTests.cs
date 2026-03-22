using System.Net;
using System.Net.Http.Json;
using MeshBoard.Contracts.Collector;
using Npgsql;

namespace MeshBoard.IntegrationTests;

public sealed class PublicCollectorStatsEndpointIntegrationTests
{
    [Fact]
    public async Task PublicCollectorStatsEndpoints_ShouldReturnHourlyPacketRollups()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();
        await SeedCollectorPacketStatsAsync(host.PersistenceConnectionString);

        var channelResponse = await client.GetAsync(
            "/api/public/collector/stats/channel-packets?serverAddress=mqtt.world.example:1883&region=US&channelName=LongFast&lookbackHours=48");
        Assert.Equal(HttpStatusCode.OK, channelResponse.StatusCode);

        var channelSnapshot = await channelResponse.Content.ReadFromJsonAsync<CollectorChannelPacketStatsSnapshot>();
        Assert.NotNull(channelSnapshot);
        Assert.Equal(2, channelSnapshot!.RowCount);
        Assert.Contains(
            channelSnapshot.Rollups,
            rollup => rollup.PacketType == "Position Update" &&
                      rollup.PacketCount == 3 &&
                      rollup.ActiveNodeCount == 2);
        Assert.Contains(
            channelSnapshot.Rollups,
            rollup => rollup.PacketType == "Telemetry" &&
                      rollup.PacketCount == 1 &&
                      rollup.ActiveNodeCount == 1);

        var nodeResponse = await client.GetAsync(
            "/api/public/collector/stats/node-packets?serverAddress=mqtt.world.example:1883&region=US&channelName=LongFast&nodeId=!alpha&lookbackHours=48");
        Assert.Equal(HttpStatusCode.OK, nodeResponse.StatusCode);

        var nodeSnapshot = await nodeResponse.Content.ReadFromJsonAsync<CollectorNodePacketStatsSnapshot>();
        Assert.NotNull(nodeSnapshot);
        Assert.Equal(2, nodeSnapshot!.RowCount);
        Assert.All(nodeSnapshot.Rollups, rollup => Assert.Equal("!alpha", rollup.NodeId));
        Assert.Contains(
            nodeSnapshot.Rollups,
            rollup => rollup.PacketType == "Position Update" &&
                      rollup.PacketCount == 2 &&
                      rollup.ShortName == "ALP" &&
                      rollup.LongName == "Alpha");
        Assert.Contains(
            nodeSnapshot.Rollups,
            rollup => rollup.PacketType == "Telemetry" &&
                      rollup.PacketCount == 1 &&
                      rollup.ShortName == "ALP");
    }

    private static async Task SeedCollectorPacketStatsAsync(string connectionString)
    {
        var observedAtUtc = DateTimeOffset.Parse("2026-03-21T10:00:00Z");
        var bucketStartUtc = DateTimeOffset.Parse("2026-03-21T10:00:00Z");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var serverCommand = new NpgsqlCommand(
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
        serverCommand.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        var serverId = (long)(await serverCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing server id."));

        await using var channelCommand = new NpgsqlCommand(
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
                'US',
                '2',
                'LongFast',
                'msh/US/2/e/LongFast/#',
                @ObservedAtUtc,
                @ObservedAtUtc)
            RETURNING id;
            """,
            connection);
        channelCommand.Parameters.AddWithValue("ServerId", serverId);
        channelCommand.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        var channelId = (long)(await channelCommand.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing channel id."));

        await InsertNodeAsync(connection, serverId, channelId, "!alpha", "ALP", "Alpha", observedAtUtc);
        await InsertNodeAsync(connection, serverId, channelId, "!bravo", "BRV", "Bravo", observedAtUtc);

        await InsertChannelRollupAsync(connection, channelId, bucketStartUtc, "Position Update", 3, observedAtUtc, observedAtUtc.AddMinutes(20));
        await InsertChannelRollupAsync(connection, channelId, bucketStartUtc, "Telemetry", 1, observedAtUtc.AddMinutes(5), observedAtUtc.AddMinutes(5));

        await InsertNodeRollupAsync(connection, channelId, bucketStartUtc, "!alpha", "Position Update", 2, observedAtUtc, observedAtUtc.AddMinutes(20));
        await InsertNodeRollupAsync(connection, channelId, bucketStartUtc, "!alpha", "Telemetry", 1, observedAtUtc.AddMinutes(5), observedAtUtc.AddMinutes(5));
        await InsertNodeRollupAsync(connection, channelId, bucketStartUtc, "!bravo", "Position Update", 1, observedAtUtc.AddMinutes(15), observedAtUtc.AddMinutes(15));
    }

    private static async Task InsertNodeAsync(
        NpgsqlConnection connection,
        long serverId,
        long channelId,
        string nodeId,
        string shortName,
        string longName,
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
                last_heard_at_utc)
            VALUES (
                @ServerId,
                @NodeId,
                @ShortName,
                @LongName,
                @ChannelId,
                @ObservedAtUtc);
            """,
            connection);
        command.Parameters.AddWithValue("ServerId", serverId);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("NodeId", nodeId);
        command.Parameters.AddWithValue("ShortName", shortName);
        command.Parameters.AddWithValue("LongName", longName);
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
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

    private static async Task InsertNodeRollupAsync(
        NpgsqlConnection connection,
        long channelId,
        DateTimeOffset bucketStartUtc,
        string nodeId,
        string packetType,
        int packetCount,
        DateTimeOffset firstSeenAtUtc,
        DateTimeOffset lastSeenAtUtc)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO collector_node_packet_hourly_rollups (
                channel_id,
                bucket_start_utc,
                node_id,
                packet_type,
                packet_count,
                first_seen_at_utc,
                last_seen_at_utc)
            VALUES (
                @ChannelId,
                @BucketStartUtc,
                @NodeId,
                @PacketType,
                @PacketCount,
                @FirstSeenAtUtc,
                @LastSeenAtUtc);
            """,
            connection);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("BucketStartUtc", bucketStartUtc);
        command.Parameters.AddWithValue("NodeId", nodeId);
        command.Parameters.AddWithValue("PacketType", packetType);
        command.Parameters.AddWithValue("PacketCount", packetCount);
        command.Parameters.AddWithValue("FirstSeenAtUtc", firstSeenAtUtc);
        command.Parameters.AddWithValue("LastSeenAtUtc", lastSeenAtUtc);
        await command.ExecuteNonQueryAsync();
    }
}
