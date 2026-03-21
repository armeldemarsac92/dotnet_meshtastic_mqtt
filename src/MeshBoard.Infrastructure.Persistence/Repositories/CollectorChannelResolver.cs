using MeshBoard.Infrastructure.Persistence.Context;
using MeshBoard.Infrastructure.Persistence.SQL;

namespace MeshBoard.Infrastructure.Persistence.Repositories;

internal sealed class CollectorChannelResolver
{
    private const string DefaultMeshVersion = "2";
    private const string UnknownRegion = "unknown";
    private const string UnknownChannelName = "unknown";
    private const string UnknownServerAddress = "unknown";

    private readonly IDbContext _dbContext;

    public CollectorChannelResolver(IDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<long> ResolveFromTopicAsync(
        string workspaceId,
        string serverAddress,
        string topic,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var identity = ParseTopicIdentity(topic);
        return ResolveAsync(workspaceId, serverAddress, identity, observedAtUtc, cancellationToken);
    }

    public Task<long> ResolveFromChannelKeyAsync(
        string workspaceId,
        string serverAddress,
        string? channelKey,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var identity = ParseChannelIdentity(channelKey);
        return ResolveAsync(workspaceId, serverAddress, identity, observedAtUtc, cancellationToken);
    }

    public Task<long> ResolveDiscoveredTopicAsync(
        string workspaceId,
        string serverAddress,
        string topicPattern,
        string region,
        string channelName,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedRegion = NormalizeSegment(region, UnknownRegion);
        var normalizedChannelName = NormalizeSegment(channelName, UnknownChannelName);
        var identity = new CollectorChannelIdentity(
            normalizedRegion,
            DefaultMeshVersion,
            normalizedChannelName,
            string.IsNullOrWhiteSpace(topicPattern)
                ? BuildTopicPattern(normalizedRegion, DefaultMeshVersion, normalizedChannelName)
                : topicPattern.Trim());

        return ResolveAsync(workspaceId, serverAddress, identity, observedAtUtc, cancellationToken);
    }

    public static string FormatChannelDisplayName(string region, string channelName)
    {
        return $"{region}/{channelName}";
    }

    private async Task<long> ResolveAsync(
        string workspaceId,
        string serverAddress,
        CollectorChannelIdentity identity,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var normalizedWorkspaceId = workspaceId.Trim();
        var normalizedServerAddress = NormalizeSegment(serverAddress, UnknownServerAddress);
        var serverId = await _dbContext.QueryFirstOrDefaultAsync<long>(
            CollectorChannelQueries.UpsertServer,
            new
            {
                WorkspaceId = normalizedWorkspaceId,
                ServerAddress = normalizedServerAddress,
                ObservedAtUtc = observedAtUtc
            },
            cancellationToken);

        return await _dbContext.QueryFirstOrDefaultAsync<long>(
            CollectorChannelQueries.UpsertChannel,
            new
            {
                WorkspaceId = normalizedWorkspaceId,
                ServerId = serverId,
                identity.Region,
                MeshVersion = identity.MeshVersion,
                ChannelName = identity.ChannelName,
                TopicPattern = identity.TopicPattern,
                ObservedAtUtc = observedAtUtc
            },
            cancellationToken);
    }

    private static CollectorChannelIdentity ParseTopicIdentity(string topic)
    {
        if (!string.IsNullOrWhiteSpace(topic))
        {
            var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length >= 5 &&
                string.Equals(segments[0], "msh", StringComparison.OrdinalIgnoreCase))
            {
                var region = NormalizeSegment(segments[1], UnknownRegion);
                var meshVersion = NormalizeSegment(segments[2], DefaultMeshVersion);
                var channelName = NormalizeSegment(segments[4], UnknownChannelName);

                return new CollectorChannelIdentity(
                    region,
                    meshVersion,
                    channelName,
                    BuildTopicPattern(region, meshVersion, channelName));
            }
        }

        return new CollectorChannelIdentity(
            UnknownRegion,
            DefaultMeshVersion,
            UnknownChannelName,
            BuildTopicPattern(UnknownRegion, DefaultMeshVersion, UnknownChannelName));
    }

    private static CollectorChannelIdentity ParseChannelIdentity(string? channelKey)
    {
        if (!string.IsNullOrWhiteSpace(channelKey))
        {
            var separatorIndex = channelKey.IndexOf('/');

            if (separatorIndex > 0 && separatorIndex < channelKey.Length - 1)
            {
                var region = NormalizeSegment(channelKey[..separatorIndex], UnknownRegion);
                var channelName = NormalizeSegment(channelKey[(separatorIndex + 1)..], UnknownChannelName);
                return new CollectorChannelIdentity(
                    region,
                    DefaultMeshVersion,
                    channelName,
                    BuildTopicPattern(region, DefaultMeshVersion, channelName));
            }
        }

        return new CollectorChannelIdentity(
            UnknownRegion,
            DefaultMeshVersion,
            UnknownChannelName,
            BuildTopicPattern(UnknownRegion, DefaultMeshVersion, UnknownChannelName));
    }

    private static string BuildTopicPattern(string region, string meshVersion, string channelName)
    {
        return $"msh/{region}/{meshVersion}/e/{channelName}/#";
    }

    private static string NormalizeSegment(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private readonly record struct CollectorChannelIdentity(
        string Region,
        string MeshVersion,
        string ChannelName,
        string TopicPattern);
}
