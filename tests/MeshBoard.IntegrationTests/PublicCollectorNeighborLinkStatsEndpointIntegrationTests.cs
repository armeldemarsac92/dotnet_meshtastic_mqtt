using System.Net;
using System.Net.Http.Json;
using MeshBoard.Contracts.Collector;
using Npgsql;

namespace MeshBoard.IntegrationTests;

public sealed class PublicCollectorNeighborLinkStatsEndpointIntegrationTests
{
    [Fact]
    public async Task PublicCollectorNeighborLinkStatsEndpoint_ShouldReturnHourlyLinkRollups()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();
        await SeedNeighborLinkStatsAsync(host.PersistenceConnectionString);

        var response = await client.GetAsync(
            "/api/public/collector/stats/neighbor-links?serverAddress=mqtt.world.example:1883&region=US&channelName=LongFast&lookbackHours=48");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await response.Content.ReadFromJsonAsync<CollectorNeighborLinkStatsSnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.RowCount);

        var rollup = Assert.Single(snapshot.Rollups);
        Assert.Equal("mqtt.world.example:1883", rollup.ServerAddress);
        Assert.Equal("US", rollup.Region);
        Assert.Equal("LongFast", rollup.ChannelName);
        Assert.Equal("!alpha", rollup.SourceNodeId);
        Assert.Equal("!bravo", rollup.TargetNodeId);
        Assert.Equal("ALP", rollup.SourceShortName);
        Assert.Equal("Alpha", rollup.SourceLongName);
        Assert.Equal("BRV", rollup.TargetShortName);
        Assert.Equal("Bravo", rollup.TargetLongName);
        Assert.Equal(3, rollup.ObservationCount);
        Assert.Equal(6.0f, rollup.AverageSnrDb);
        Assert.Equal(7.5f, rollup.MaxSnrDb);
        Assert.Equal(7.5f, rollup.LastSnrDb);
    }

    private static async Task SeedNeighborLinkStatsAsync(string connectionString)
    {
        var observedAtUtc = DateTimeOffset.Parse("2026-03-21T11:00:00Z");
        var bucketStartUtc = DateTimeOffset.Parse("2026-03-21T11:00:00Z");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var serverCommand = new NpgsqlCommand(
            """
            INSERT INTO collector_servers (
                workspace_id,
                server_address,
                first_observed_at_utc,
                last_observed_at_utc)
            VALUES (
                'default',
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
                workspace_id,
                server_id,
                region,
                mesh_version,
                channel_name,
                topic_pattern,
                first_observed_at_utc,
                last_observed_at_utc)
            VALUES (
                'default',
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

        await InsertNodeAsync(connection, channelId, "!alpha", "ALP", "Alpha", observedAtUtc);
        await InsertNodeAsync(connection, channelId, "!bravo", "BRV", "Bravo", observedAtUtc);

        await using var rollupCommand = new NpgsqlCommand(
            """
            INSERT INTO collector_neighbor_link_hourly_rollups (
                workspace_id,
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
                'default',
                @ChannelId,
                @BucketStartUtc,
                '!alpha',
                '!bravo',
                3,
                3,
                18.0,
                7.5,
                7.5,
                @ObservedAtUtc,
                @LastSeenAtUtc);
            """,
            connection);
        rollupCommand.Parameters.AddWithValue("ChannelId", channelId);
        rollupCommand.Parameters.AddWithValue("BucketStartUtc", bucketStartUtc);
        rollupCommand.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        rollupCommand.Parameters.AddWithValue("LastSeenAtUtc", observedAtUtc.AddMinutes(20));
        await rollupCommand.ExecuteNonQueryAsync();
    }

    private static async Task InsertNodeAsync(
        NpgsqlConnection connection,
        long channelId,
        string nodeId,
        string shortName,
        string longName,
        DateTimeOffset observedAtUtc)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO collector_nodes (
                workspace_id,
                channel_id,
                node_id,
                short_name,
                long_name,
                last_heard_at_utc)
            VALUES (
                'default',
                @ChannelId,
                @NodeId,
                @ShortName,
                @LongName,
                @ObservedAtUtc);
            """,
            connection);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("NodeId", nodeId);
        command.Parameters.AddWithValue("ShortName", shortName);
        command.Parameters.AddWithValue("LongName", longName);
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        await command.ExecuteNonQueryAsync();
    }
}
