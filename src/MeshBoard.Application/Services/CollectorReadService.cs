using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Workspaces;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface ICollectorReadService
{
    Task<IReadOnlyCollection<CollectorServerSummary>> GetObservedServers(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CollectorChannelSummary>> GetObservedChannels(
        CollectorMapQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<CollectorMapSnapshot> GetMapSnapshot(
        CollectorMapQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<CollectorChannelPacketStatsSnapshot> GetChannelPacketStats(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<CollectorNodePacketStatsSnapshot> GetNodePacketStats(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default);
}

public sealed class CollectorReadService : ICollectorReadService
{
    private const int DefaultActiveWithinHours = 24;
    private const int MaxActiveWithinHours = 24 * 14;
    private const int DefaultMaxNodes = 5_000;
    private const int MaxNodes = 10_000;
    private const int DefaultMaxLinks = 10_000;
    private const int MaxLinks = 20_000;
    private const int DefaultStatsLookbackHours = 24 * 7;
    private const int MaxStatsLookbackHours = 24 * 30;
    private const int DefaultStatsMaxRows = 500;
    private const int MaxStatsRows = 5_000;

    private readonly ICollectorReadRepository _collectorReadRepository;
    private readonly ILogger<CollectorReadService> _logger;
    private readonly TimeProvider _timeProvider;

    public CollectorReadService(
        ICollectorReadRepository collectorReadRepository,
        TimeProvider timeProvider,
        ILogger<CollectorReadService> logger)
    {
        _collectorReadRepository = collectorReadRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<IReadOnlyCollection<CollectorServerSummary>> GetObservedServers(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to fetch observed collector servers");
        return _collectorReadRepository.GetServersAsync(WorkspaceConstants.DefaultWorkspaceId, cancellationToken);
    }

    public Task<IReadOnlyCollection<CollectorChannelSummary>> GetObservedChannels(
        CollectorMapQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = SanitizeQuery(query);

        _logger.LogDebug(
            "Attempting to fetch observed collector channels for server {ServerAddress}, region {Region}, channel {ChannelName}",
            sanitizedQuery.ServerAddress,
            sanitizedQuery.Region,
            sanitizedQuery.ChannelName);

        return _collectorReadRepository.GetChannelsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery,
            cancellationToken);
    }

    public async Task<CollectorMapSnapshot> GetMapSnapshot(
        CollectorMapQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = SanitizeQuery(query);
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var notBeforeUtc = generatedAtUtc.AddHours(-sanitizedQuery.ActiveWithinHours);

        _logger.LogDebug(
            "Attempting to fetch collector map snapshot for server {ServerAddress}, region {Region}, channel {ChannelName}, active within {ActiveWithinHours} hours",
            sanitizedQuery.ServerAddress,
            sanitizedQuery.Region,
            sanitizedQuery.ChannelName,
            sanitizedQuery.ActiveWithinHours);

        var channelsTask = _collectorReadRepository.GetChannelsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery,
            cancellationToken);
        var nodesTask = _collectorReadRepository.GetMapNodesAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery,
            notBeforeUtc,
            cancellationToken);
        var linksTask = _collectorReadRepository.GetMapLinksAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery,
            notBeforeUtc,
            cancellationToken);

        await Task.WhenAll(channelsTask, nodesTask, linksTask);

        var channels = await channelsTask;
        var nodes = await nodesTask;
        var links = await linksTask;

        return new CollectorMapSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
            ServerAddress = NullIfEmpty(sanitizedQuery.ServerAddress),
            Region = NullIfEmpty(sanitizedQuery.Region),
            ChannelName = NullIfEmpty(sanitizedQuery.ChannelName),
            ActiveWithinHours = sanitizedQuery.ActiveWithinHours,
            ServerCount = channels
                .Select(channel => channel.ServerAddress)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ChannelCount = channels.Count,
            NodeCount = nodes.Count,
            LinkCount = links.Count,
            Nodes = nodes,
            Links = links
        };
    }

    public async Task<CollectorChannelPacketStatsSnapshot> GetChannelPacketStats(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = SanitizePacketStatsQuery(query);
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var notBeforeUtc = generatedAtUtc.AddHours(-sanitizedQuery.LookbackHours);

        _logger.LogDebug(
            "Attempting to fetch collector channel packet stats for server {ServerAddress}, region {Region}, channel {ChannelName}, packet type {PacketType}, lookback {LookbackHours} hours",
            sanitizedQuery.ServerAddress,
            sanitizedQuery.Region,
            sanitizedQuery.ChannelName,
            sanitizedQuery.PacketType,
            sanitizedQuery.LookbackHours);

        var rollups = await _collectorReadRepository.GetChannelPacketRollupsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery,
            notBeforeUtc,
            cancellationToken);

        return new CollectorChannelPacketStatsSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
            ServerAddress = NullIfEmpty(sanitizedQuery.ServerAddress),
            Region = NullIfEmpty(sanitizedQuery.Region),
            ChannelName = NullIfEmpty(sanitizedQuery.ChannelName),
            PacketType = NullIfEmpty(sanitizedQuery.PacketType),
            LookbackHours = sanitizedQuery.LookbackHours,
            RowCount = rollups.Count,
            Rollups = rollups
        };
    }

    public async Task<CollectorNodePacketStatsSnapshot> GetNodePacketStats(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = SanitizePacketStatsQuery(query);
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var notBeforeUtc = generatedAtUtc.AddHours(-sanitizedQuery.LookbackHours);

        _logger.LogDebug(
            "Attempting to fetch collector node packet stats for server {ServerAddress}, region {Region}, channel {ChannelName}, node {NodeId}, packet type {PacketType}, lookback {LookbackHours} hours",
            sanitizedQuery.ServerAddress,
            sanitizedQuery.Region,
            sanitizedQuery.ChannelName,
            sanitizedQuery.NodeId,
            sanitizedQuery.PacketType,
            sanitizedQuery.LookbackHours);

        var rollups = await _collectorReadRepository.GetNodePacketRollupsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery,
            notBeforeUtc,
            cancellationToken);

        return new CollectorNodePacketStatsSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            WorkspaceId = WorkspaceConstants.DefaultWorkspaceId,
            ServerAddress = NullIfEmpty(sanitizedQuery.ServerAddress),
            Region = NullIfEmpty(sanitizedQuery.Region),
            ChannelName = NullIfEmpty(sanitizedQuery.ChannelName),
            NodeId = NullIfEmpty(sanitizedQuery.NodeId),
            PacketType = NullIfEmpty(sanitizedQuery.PacketType),
            LookbackHours = sanitizedQuery.LookbackHours,
            RowCount = rollups.Count,
            Rollups = rollups
        };
    }

    private static CollectorMapQuery SanitizeQuery(CollectorMapQuery? query)
    {
        return new CollectorMapQuery
        {
            ServerAddress = Normalize(query?.ServerAddress),
            Region = Normalize(query?.Region),
            ChannelName = Normalize(query?.ChannelName),
            ActiveWithinHours = Clamp(query?.ActiveWithinHours ?? DefaultActiveWithinHours, 1, MaxActiveWithinHours),
            MaxNodes = Clamp(query?.MaxNodes ?? DefaultMaxNodes, 1, MaxNodes),
            MaxLinks = Clamp(query?.MaxLinks ?? DefaultMaxLinks, 1, MaxLinks)
        };
    }

    private static CollectorPacketStatsQuery SanitizePacketStatsQuery(CollectorPacketStatsQuery? query)
    {
        return new CollectorPacketStatsQuery
        {
            ServerAddress = Normalize(query?.ServerAddress),
            Region = Normalize(query?.Region),
            ChannelName = Normalize(query?.ChannelName),
            NodeId = Normalize(query?.NodeId),
            PacketType = Normalize(query?.PacketType),
            LookbackHours = Clamp(query?.LookbackHours ?? DefaultStatsLookbackHours, 1, MaxStatsLookbackHours),
            MaxRows = Clamp(query?.MaxRows ?? DefaultStatsMaxRows, 1, MaxStatsRows)
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return Math.Min(value, max);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
