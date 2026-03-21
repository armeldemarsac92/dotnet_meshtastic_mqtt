using System.Net;
using System.Net.Http.Json;
using MeshBoard.Contracts.Collector;
using Npgsql;

namespace MeshBoard.IntegrationTests;

public sealed class PublicCollectorTopologyEndpointIntegrationTests
{
    [Fact]
    public async Task PublicCollectorTopologyEndpoint_ShouldReturnGraphSummary()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();
        await SeedTopologySnapshotAsync(host.PersistenceConnectionString);

        var response = await client.GetAsync(
            "/api/public/collector/topology?serverAddress=mqtt.world.example:1883&region=US&channelName=LongFast&activeWithinHours=48&topCount=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snapshot = await response.Content.ReadFromJsonAsync<CollectorTopologySnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(5, snapshot!.NodeCount);
        Assert.Equal(3, snapshot.LinkCount);
        Assert.Equal(2, snapshot.ConnectedComponentCount);
        Assert.Equal(4, snapshot.LargestConnectedComponentSize);
        Assert.Equal(1, snapshot.IsolatedNodeCount);
        Assert.Equal(2, snapshot.BridgeNodeCount);

        Assert.Collection(
            snapshot.Components,
            component =>
            {
                Assert.Equal(4, component.NodeCount);
                Assert.Equal(3, component.LinkCount);
                Assert.Contains("!alpha", component.SampleNodeIds);
            },
            component =>
            {
                Assert.Equal(1, component.NodeCount);
                Assert.Equal(0, component.LinkCount);
                Assert.Contains("!echo", component.SampleNodeIds);
            });

        Assert.Contains(snapshot.TopDegreeNodes, node => node.NodeId == "!bravo" && node.Degree == 2 && node.ComponentSize == 4);
        Assert.Contains(snapshot.TopDegreeNodes, node => node.NodeId == "!charlie" && node.Degree == 2 && node.ComponentSize == 4);
        Assert.Contains(snapshot.BridgeNodes, node => node.NodeId == "!bravo");
        Assert.Contains(snapshot.BridgeNodes, node => node.NodeId == "!charlie");

        var strongestLink = Assert.Single(snapshot.StrongestLinks, link => link.SourceNodeId == "!alpha" && link.TargetNodeId == "!bravo");
        Assert.Equal(10, strongestLink.ObservationCount);
        Assert.Equal(6.0f, strongestLink.AverageSnrDb);
        Assert.Equal(7.0f, strongestLink.MaxSnrDb);
        Assert.Equal(7.0f, strongestLink.LastSnrDb);
        Assert.Equal("ALP", strongestLink.SourceShortName);
        Assert.Equal("Bravo", strongestLink.TargetLongName);
    }

    private static async Task SeedTopologySnapshotAsync(string connectionString)
    {
        var observedAtUtc = DateTimeOffset.Parse("2026-03-22T09:00:00Z");
        var bucketStartUtc = DateTimeOffset.Parse("2026-03-22T09:00:00Z");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var serverId = await InsertServerAsync(connection, observedAtUtc);
        var channelId = await InsertChannelAsync(connection, serverId, observedAtUtc);

        await InsertNodeAsync(connection, channelId, "!alpha", "ALP", "Alpha", observedAtUtc);
        await InsertNodeAsync(connection, channelId, "!bravo", "BRV", "Bravo", observedAtUtc);
        await InsertNodeAsync(connection, channelId, "!charlie", "CHR", "Charlie", observedAtUtc);
        await InsertNodeAsync(connection, channelId, "!delta", "DLT", "Delta", observedAtUtc);
        await InsertNodeAsync(connection, channelId, "!echo", "ECH", "Echo", observedAtUtc);

        await InsertCurrentLinkAsync(connection, channelId, "!alpha", "!bravo", 6.5f, observedAtUtc);
        await InsertCurrentLinkAsync(connection, channelId, "!bravo", "!charlie", 4.0f, observedAtUtc.AddMinutes(5));
        await InsertCurrentLinkAsync(connection, channelId, "!charlie", "!delta", 2.0f, observedAtUtc.AddMinutes(10));

        await InsertLinkRollupAsync(connection, channelId, bucketStartUtc, "!alpha", "!bravo", 10, 60.0, 7.0f, 7.0f, observedAtUtc, observedAtUtc.AddMinutes(15));
        await InsertLinkRollupAsync(connection, channelId, bucketStartUtc, "!bravo", "!charlie", 8, 20.0, 4.5f, 4.0f, observedAtUtc.AddMinutes(1), observedAtUtc.AddMinutes(16));
        await InsertLinkRollupAsync(connection, channelId, bucketStartUtc, "!charlie", "!delta", 2, 4.0, 2.5f, 2.0f, observedAtUtc.AddMinutes(2), observedAtUtc.AddMinutes(17));
    }

    private static async Task<long> InsertServerAsync(NpgsqlConnection connection, DateTimeOffset observedAtUtc)
    {
        await using var command = new NpgsqlCommand(
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
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing server id."));
    }

    private static async Task<long> InsertChannelAsync(NpgsqlConnection connection, long serverId, DateTimeOffset observedAtUtc)
    {
        await using var command = new NpgsqlCommand(
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
        command.Parameters.AddWithValue("ServerId", serverId);
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Missing channel id."));
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
                workspace_id,
                channel_id,
                source_node_id,
                target_node_id,
                snr_db,
                last_seen_at_utc)
            VALUES (
                'default',
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
