using System.Net;
using System.Net.Http.Json;
using MeshBoard.Contracts.Collector;
using Npgsql;

namespace MeshBoard.IntegrationTests;

public sealed class PublicCollectorEndpointIntegrationTests
{
    [Fact]
    public async Task PublicCollectorEndpoints_ShouldReturnObservedServersChannelsAndSnapshot()
    {
        await using var host = new ApiIntegrationTestHost();
        using var client = host.CreateApiClient();
        await SeedCollectorSnapshotAsync(host.PersistenceConnectionString);

        var serversResponse = await client.GetAsync("/api/public/collector/servers");
        Assert.Equal(HttpStatusCode.OK, serversResponse.StatusCode);

        var servers = await serversResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<CollectorServerSummary>>();
        var server = Assert.Single(servers!);
        Assert.Equal("mqtt.world.example:1883", server.ServerAddress);
        Assert.Equal(1, server.ChannelCount);
        Assert.Equal(2, server.NodeCount);
        Assert.Equal(1, server.NeighborLinkCount);

        var channelsResponse = await client.GetAsync("/api/public/collector/channels?serverAddress=mqtt.world.example:1883");
        Assert.Equal(HttpStatusCode.OK, channelsResponse.StatusCode);

        var channels = await channelsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<CollectorChannelSummary>>();
        var channel = Assert.Single(channels!);
        Assert.Equal("US", channel.Region);
        Assert.Equal("LongFast", channel.ChannelName);
        Assert.Equal(2, channel.NodeCount);
        Assert.Equal(1, channel.NeighborLinkCount);

        var snapshotResponse = await client.GetAsync(
            "/api/public/collector/snapshot?serverAddress=mqtt.world.example:1883&region=US&channelName=LongFast&activeWithinHours=48");
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

        var snapshot = await snapshotResponse.Content.ReadFromJsonAsync<CollectorMapSnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal("default", snapshot!.WorkspaceId);
        Assert.Equal(1, snapshot.ServerCount);
        Assert.Equal(1, snapshot.ChannelCount);
        Assert.Equal(2, snapshot.NodeCount);
        Assert.Equal(1, snapshot.LinkCount);
        Assert.Contains(snapshot.Nodes, node => node.NodeId == "!alpha" && node.BrokerServer == "mqtt.world.example:1883");
        Assert.Contains(snapshot.Nodes, node => node.NodeId == "!bravo" && node.LastHeardChannel == "US/LongFast");

        var link = Assert.Single(snapshot.Links);
        Assert.Equal("!alpha", link.SourceNodeId);
        Assert.Equal("!bravo", link.TargetNodeId);
        Assert.Equal("mqtt.world.example:1883", link.ServerAddress);
        Assert.Equal("US", link.Region);
        Assert.Equal("LongFast", link.ChannelName);
        Assert.Equal(7.25f, link.SnrDb);
    }

    private static async Task SeedCollectorSnapshotAsync(string connectionString)
    {
        var observedAtUtc = DateTimeOffset.UtcNow.AddHours(-1);

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

        await InsertNodeAsync(connection, channelId, "!alpha", "ALP", "Alpha", 48.8566, 2.3522, observedAtUtc);
        await InsertNodeAsync(connection, channelId, "!bravo", "BRV", "Bravo", 45.7640, 4.8357, observedAtUtc.AddMinutes(-5));

        await using var linkCommand = new NpgsqlCommand(
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
                '!alpha',
                '!bravo',
                7.25,
                @ObservedAtUtc);
            """,
            connection);
        linkCommand.Parameters.AddWithValue("ChannelId", channelId);
        linkCommand.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        await linkCommand.ExecuteNonQueryAsync();
    }

    private static async Task InsertNodeAsync(
        NpgsqlConnection connection,
        long channelId,
        string nodeId,
        string shortName,
        string longName,
        double latitude,
        double longitude,
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
                last_heard_at_utc,
                last_text_message_at_utc,
                last_known_latitude,
                last_known_longitude)
            VALUES (
                'default',
                @ChannelId,
                @NodeId,
                @ShortName,
                @LongName,
                @ObservedAtUtc,
                @ObservedAtUtc,
                @Latitude,
                @Longitude);
            """,
            connection);
        command.Parameters.AddWithValue("ChannelId", channelId);
        command.Parameters.AddWithValue("NodeId", nodeId);
        command.Parameters.AddWithValue("ShortName", shortName);
        command.Parameters.AddWithValue("LongName", longName);
        command.Parameters.AddWithValue("ObservedAtUtc", observedAtUtc);
        command.Parameters.AddWithValue("Latitude", latitude);
        command.Parameters.AddWithValue("Longitude", longitude);
        await command.ExecuteNonQueryAsync();
    }
}
