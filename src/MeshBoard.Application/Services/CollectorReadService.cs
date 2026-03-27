using MeshBoard.Application.Abstractions.Collector;
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

    Task<CollectorNeighborLinkStatsSnapshot> GetNeighborLinkStats(
        CollectorNeighborLinkStatsQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<CollectorTopologySnapshot> GetTopologySnapshot(
        CollectorTopologyQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<CollectorOverviewSnapshot> GetOverviewSnapshot(
        CollectorOverviewQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<CollectorNodePage> GetNodePage(
        CollectorNodePageQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<CollectorChannelPage> GetChannelPage(
        CollectorChannelPageQuery? query = null,
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
    private const int DefaultTopologyTopCount = 10;
    private const int MaxTopologyTopCount = 5_000;
    private const int MaxTopologyRollupRows = 20_000;
    private const int DefaultOverviewMaxChannels = 20;
    private const int MaxOverviewMaxChannels = 100;
    private const int DefaultOverviewTopPacketTypes = 3;
    private const int MaxOverviewTopPacketTypes = 20;

    private readonly ICollectorReadRepository _collectorReadRepository;
    private readonly ITopologyReadAdapter _topologyReadAdapter;
    private readonly ILogger<CollectorReadService> _logger;
    private readonly TimeProvider _timeProvider;

    public CollectorReadService(
        ICollectorReadRepository collectorReadRepository,
        ITopologyReadAdapter topologyReadAdapter,
        TimeProvider timeProvider,
        ILogger<CollectorReadService> logger)
    {
        _collectorReadRepository = collectorReadRepository;
        _topologyReadAdapter = topologyReadAdapter;
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
        var sanitizedQuery = query.ToSanitizedCollectorMapQuery(
            DefaultActiveWithinHours,
            MaxActiveWithinHours,
            DefaultMaxNodes,
            MaxNodes,
            DefaultMaxLinks,
            MaxLinks);

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
        var sanitizedQuery = query.ToSanitizedCollectorMapQuery(
            DefaultActiveWithinHours,
            MaxActiveWithinHours,
            DefaultMaxNodes,
            MaxNodes,
            DefaultMaxLinks,
            MaxLinks);
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

        return sanitizedQuery.ToCollectorMapSnapshot(generatedAtUtc, channels, nodes, links);
    }

    public async Task<CollectorChannelPacketStatsSnapshot> GetChannelPacketStats(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query.ToSanitizedCollectorPacketStatsQuery(
            DefaultStatsLookbackHours,
            MaxStatsLookbackHours,
            DefaultStatsMaxRows,
            MaxStatsRows);
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

        return sanitizedQuery.ToCollectorChannelPacketStatsSnapshot(generatedAtUtc, rollups);
    }

    public async Task<CollectorNodePacketStatsSnapshot> GetNodePacketStats(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query.ToSanitizedCollectorPacketStatsQuery(
            DefaultStatsLookbackHours,
            MaxStatsLookbackHours,
            DefaultStatsMaxRows,
            MaxStatsRows);
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

        return sanitizedQuery.ToCollectorNodePacketStatsSnapshot(generatedAtUtc, rollups);
    }

    public async Task<CollectorNeighborLinkStatsSnapshot> GetNeighborLinkStats(
        CollectorNeighborLinkStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query.ToSanitizedCollectorNeighborLinkStatsQuery(
            DefaultStatsLookbackHours,
            MaxStatsLookbackHours,
            DefaultStatsMaxRows,
            MaxStatsRows);
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var notBeforeUtc = generatedAtUtc.AddHours(-sanitizedQuery.LookbackHours);

        _logger.LogDebug(
            "Attempting to fetch collector neighbor-link stats for server {ServerAddress}, region {Region}, channel {ChannelName}, source {SourceNodeId}, target {TargetNodeId}, lookback {LookbackHours} hours",
            sanitizedQuery.ServerAddress,
            sanitizedQuery.Region,
            sanitizedQuery.ChannelName,
            sanitizedQuery.SourceNodeId,
            sanitizedQuery.TargetNodeId,
            sanitizedQuery.LookbackHours);

        var rollups = await _collectorReadRepository.GetNeighborLinkRollupsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery,
            notBeforeUtc,
            cancellationToken);

        return sanitizedQuery.ToCollectorNeighborLinkStatsSnapshot(generatedAtUtc, rollups);
    }

    public async Task<CollectorTopologySnapshot> GetTopologySnapshot(
        CollectorTopologyQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query.ToSanitizedCollectorTopologyQuery(
            DefaultActiveWithinHours,
            MaxActiveWithinHours,
            MaxNodes,
            MaxNodes,
            MaxLinks,
            MaxLinks,
            DefaultTopologyTopCount,
            MaxTopologyTopCount);
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var notBeforeUtc = generatedAtUtc.AddHours(-sanitizedQuery.ActiveWithinHours);

        _logger.LogDebug(
            "Attempting to fetch collector topology snapshot for server {ServerAddress}, region {Region}, channel {ChannelName}, active within {ActiveWithinHours} hours",
            sanitizedQuery.ServerAddress,
            sanitizedQuery.Region,
            sanitizedQuery.ChannelName,
            sanitizedQuery.ActiveWithinHours);

        var topologyNodesTask = _topologyReadAdapter.GetTopologyNodesAsync(
            sanitizedQuery,
            notBeforeUtc,
            cancellationToken);
        var topologyLinksTask = _topologyReadAdapter.GetTopologyLinksAsync(
            sanitizedQuery,
            notBeforeUtc,
            cancellationToken);
        var rollupsTask = _collectorReadRepository.GetNeighborLinkRollupsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            sanitizedQuery.ToCollectorNeighborLinkStatsQuery(MaxTopologyRollupRows),
            notBeforeUtc,
            cancellationToken);

        await Task.WhenAll(topologyNodesTask, topologyLinksTask, rollupsTask);

        var nodes = (await topologyNodesTask)
            .DistinctBy(n => n.NodeId, StringComparer.Ordinal)
            .ToArray();
        var links = await topologyLinksTask;
        var rollups = await rollupsTask;
        var analysis = AnalyzeTopology(nodes, links, rollups, sanitizedQuery.TopCount);

        var components = analysis.Components
            .Take(sanitizedQuery.TopCount)
            .Select((component, index) => component.NodeIds.ToCollectorTopologyComponentSummary(index + 1, component.LinkCount))
            .ToArray();
        var topDegreeNodes = nodes
            .OrderByDescending(node => analysis.Degrees.GetValueOrDefault(node.NodeId))
            .ThenBy(node => node.LongName ?? node.ShortName ?? node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .Take(sanitizedQuery.TopCount)
            .Select(node => node.ToCollectorTopologyNodeSummary(
                analysis.Degrees.GetValueOrDefault(node.NodeId),
                analysis.ComponentSizes.GetValueOrDefault(node.NodeId)))
            .ToArray();
        var bridgeNodes = analysis.ArticulationNodeIds
            .Select(nodeId => analysis.NodeById.GetValueOrDefault(nodeId))
            .Where(node => node is not null)
            .Select(node => node!)
            .OrderByDescending(node => analysis.Degrees.GetValueOrDefault(node.NodeId))
            .ThenBy(node => node.LongName ?? node.ShortName ?? node.NodeId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .Take(sanitizedQuery.TopCount)
            .Select(node => node.ToCollectorTopologyNodeSummary(
                analysis.Degrees.GetValueOrDefault(node.NodeId),
                analysis.ComponentSizes.GetValueOrDefault(node.NodeId)))
            .ToArray();

        return sanitizedQuery.ToCollectorTopologySnapshot(
            generatedAtUtc,
            nodes,
            links,
            analysis.ConnectedComponentCount,
            analysis.LargestConnectedComponentSize,
            analysis.IsolatedNodeCount,
            analysis.BridgeNodeCount,
            components,
            topDegreeNodes,
            bridgeNodes,
            analysis.StrongestLinks);
    }

    public async Task<CollectorOverviewSnapshot> GetOverviewSnapshot(
        CollectorOverviewQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query.ToSanitizedCollectorOverviewQuery(
            DefaultActiveWithinHours,
            MaxActiveWithinHours,
            DefaultStatsLookbackHours,
            MaxStatsLookbackHours,
            DefaultOverviewMaxChannels,
            MaxOverviewMaxChannels,
            DefaultOverviewTopPacketTypes,
            MaxOverviewTopPacketTypes);
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var activeNotBeforeUtc = generatedAtUtc.AddHours(-sanitizedQuery.ActiveWithinHours);
        var lookbackNotBeforeUtc = generatedAtUtc.AddHours(-sanitizedQuery.LookbackHours);

        _logger.LogDebug(
            "Attempting to fetch collector overview for server {ServerAddress}, region {Region}, channel {ChannelName}, active within {ActiveWithinHours} hours, lookback {LookbackHours} hours",
            sanitizedQuery.ServerAddress,
            sanitizedQuery.Region,
            sanitizedQuery.ChannelName,
            sanitizedQuery.ActiveWithinHours,
            sanitizedQuery.LookbackHours);

        var channelFilter = sanitizedQuery.ToCollectorMapQuery(MaxNodes, MaxLinks);

        var channels = await _collectorReadRepository.GetChannelsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            channelFilter,
            cancellationToken);

        var selectedChannels = channels
            .OrderByDescending(channel => channel.LastObservedAtUtc)
            .ThenBy(channel => channel.ServerAddress, StringComparer.Ordinal)
            .ThenBy(channel => channel.Region, StringComparer.Ordinal)
            .ThenBy(channel => channel.ChannelName, StringComparer.Ordinal)
            .Take(sanitizedQuery.MaxChannels)
            .ToArray();

        if (selectedChannels.Length == 0)
        {
            return sanitizedQuery.ToEmptyCollectorOverviewSnapshot(generatedAtUtc);
        }

        var channelContexts = await Task.WhenAll(
            selectedChannels.Select(channel => BuildOverviewChannelAsync(
                channel,
                sanitizedQuery,
                activeNotBeforeUtc,
                lookbackNotBeforeUtc,
                cancellationToken)));

        var selectedChannelContextsByServer = channelContexts
            .GroupBy(context => context.Channel.ServerAddress, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<OverviewChannelContext>)group
                    .OrderByDescending(context => context.Channel.LastObservedAtUtc)
                    .ThenBy(context => context.Channel.Region, StringComparer.Ordinal)
                    .ThenBy(context => context.Channel.ChannelName, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        var serverSummaries = new List<CollectorOverviewServerSummary>();

        foreach (var serverGroup in channels
                     .GroupBy(channel => channel.ServerAddress, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            selectedChannelContextsByServer.TryGetValue(serverGroup.Key, out var selectedServerChannels);

            serverSummaries.Add(
                await BuildOverviewServerAsync(
                    serverGroup.Key,
                    serverGroup.ToArray(),
                    selectedServerChannels ?? Array.Empty<OverviewChannelContext>(),
                    sanitizedQuery,
                    activeNotBeforeUtc,
                    lookbackNotBeforeUtc,
                    cancellationToken));
        }

        return sanitizedQuery.ToCollectorOverviewSnapshot(generatedAtUtc, channels, serverSummaries);
    }

    private async Task<CollectorOverviewServerSummary> BuildOverviewServerAsync(
        string serverAddress,
        IReadOnlyCollection<CollectorChannelSummary> channels,
        IReadOnlyCollection<OverviewChannelContext> selectedChannelContexts,
        CollectorOverviewQuery query,
        DateTimeOffset activeNotBeforeUtc,
        DateTimeOffset lookbackNotBeforeUtc,
        CancellationToken cancellationToken)
    {
        var topologyQuery = query.ToCollectorTopologyQuery(serverAddress, DefaultTopologyTopCount, MaxNodes, MaxLinks);
        var packetStatsQuery = query.ToCollectorPacketStatsQuery(serverAddress, MaxStatsRows);
        var neighborLinkQuery = query.ToCollectorNeighborLinkStatsQuery(serverAddress, MaxStatsRows);

        var nodes = await _topologyReadAdapter.GetTopologyNodesAsync(
            topologyQuery,
            activeNotBeforeUtc,
            cancellationToken);
        var links = await _topologyReadAdapter.GetTopologyLinksAsync(
            topologyQuery,
            activeNotBeforeUtc,
            cancellationToken);
        var packetTypeTotals = await _collectorReadRepository.GetChannelPacketTypeTotalsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            packetStatsQuery,
            lookbackNotBeforeUtc,
            cancellationToken);
        var neighborObservationCount = await _collectorReadRepository.GetNeighborObservationCountAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            neighborLinkQuery,
            lookbackNotBeforeUtc,
            cancellationToken);

        return channels.ToCollectorOverviewServerSummary(
            serverAddress,
            selectedChannelContexts.Select(context => context.Summary).ToArray(),
            nodes,
            links,
            packetTypeTotals,
            neighborObservationCount);
    }

    private async Task<OverviewChannelContext> BuildOverviewChannelAsync(
        CollectorChannelSummary channel,
        CollectorOverviewQuery query,
        DateTimeOffset activeNotBeforeUtc,
        DateTimeOffset lookbackNotBeforeUtc,
        CancellationToken cancellationToken)
    {
        var topologyQuery = channel.ToCollectorTopologyQuery(query, DefaultTopologyTopCount, MaxNodes, MaxLinks);
        var packetStatsQuery = channel.ToCollectorPacketStatsQuery(query, MaxStatsRows);
        var neighborLinkQuery = channel.ToCollectorNeighborLinkStatsQuery(query, MaxStatsRows);

        var nodes = await _collectorReadRepository.GetTopologyNodesAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            topologyQuery,
            activeNotBeforeUtc,
            cancellationToken);
        var links = await _collectorReadRepository.GetTopologyLinksAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            topologyQuery,
            activeNotBeforeUtc,
            cancellationToken);
        var packetTypeTotals = await _collectorReadRepository.GetChannelPacketTypeTotalsAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            packetStatsQuery,
            lookbackNotBeforeUtc,
            cancellationToken);
        var neighborObservationCount = await _collectorReadRepository.GetNeighborObservationCountAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            neighborLinkQuery,
            lookbackNotBeforeUtc,
            cancellationToken);

        var analysis = AnalyzeTopology(nodes, links, Array.Empty<CollectorNeighborLinkHourlyRollup>(), 0);

        return new OverviewChannelContext(
            channel,
            nodes,
            channel.ToCollectorOverviewChannelSummary(
                nodes,
                links,
                analysis.ConnectedComponentCount,
                analysis.LargestConnectedComponentSize,
                analysis.IsolatedNodeCount,
                analysis.BridgeNodeCount,
                packetTypeTotals.Sum(total => total.PacketCount),
                packetTypeTotals
                    .OrderByDescending(total => total.PacketCount)
                    .ThenBy(total => total.PacketType, StringComparer.Ordinal)
                    .Take(query.TopPacketTypes)
                    .ToArray(),
                neighborObservationCount));
    }

    public Task<CollectorNodePage> GetNodePage(
        CollectorNodePageQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitized = query.ToSanitizedCollectorNodePageQuery(50, 200);

        return _collectorReadRepository.GetNodePageAsync(sanitized, cancellationToken);
    }

    public Task<CollectorChannelPage> GetChannelPage(
        CollectorChannelPageQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitized = query.ToSanitizedCollectorChannelPageQuery(50, 200);

        return _collectorReadRepository.GetChannelPageAsync(sanitized, cancellationToken);
    }

    private static TopologyAnalysis AnalyzeTopology(
        IReadOnlyCollection<MeshBoard.Contracts.Nodes.NodeSummary> nodes,
        IReadOnlyCollection<CollectorMapLinkSummary> links,
        IReadOnlyCollection<CollectorNeighborLinkHourlyRollup> rollups,
        int strongestLinkCount)
    {
        var nodeById = nodes.DistinctBy(node => node.NodeId, StringComparer.Ordinal).ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var adjacency = BuildAdjacency(nodes.Select(node => node.NodeId), links);
        var components = BuildComponents(adjacency);
        var componentSizes = components
            .SelectMany(component => component.NodeIds.Select(nodeId => new KeyValuePair<string, int>(nodeId, component.NodeCount)))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var degrees = adjacency.ToDictionary(entry => entry.Key, entry => entry.Value.Count, StringComparer.Ordinal);
        var articulationNodeIds = FindArticulationPoints(adjacency);

        return new TopologyAnalysis(
            nodeById,
            components,
            componentSizes,
            degrees,
            articulationNodeIds,
            components.Count,
            components.Count == 0 ? 0 : components.Max(component => component.NodeCount),
            degrees.Count(entry => entry.Value == 0),
            articulationNodeIds.Count,
            strongestLinkCount > 0
                ? BuildStrongestLinks(links, rollups, nodeById, strongestLinkCount)
                : Array.Empty<CollectorTopologyLinkSummary>());
    }

    private static Dictionary<string, HashSet<string>> BuildAdjacency(
        IEnumerable<string> nodeIds,
        IReadOnlyCollection<CollectorMapLinkSummary> links)
    {
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var nodeId in nodeIds)
        {
            if (!adjacency.ContainsKey(nodeId))
            {
                adjacency[nodeId] = new HashSet<string>(StringComparer.Ordinal);
            }
        }

        foreach (var link in links)
        {
            if (!adjacency.TryGetValue(link.SourceNodeId, out var sourceNeighbors))
            {
                sourceNeighbors = new HashSet<string>(StringComparer.Ordinal);
                adjacency[link.SourceNodeId] = sourceNeighbors;
            }

            if (!adjacency.TryGetValue(link.TargetNodeId, out var targetNeighbors))
            {
                targetNeighbors = new HashSet<string>(StringComparer.Ordinal);
                adjacency[link.TargetNodeId] = targetNeighbors;
            }

            sourceNeighbors.Add(link.TargetNodeId);
            targetNeighbors.Add(link.SourceNodeId);
        }

        return adjacency;
    }

    private static IReadOnlyCollection<GraphComponent> BuildComponents(
        IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<GraphComponent>();

        foreach (var nodeId in adjacency.Keys.Order(StringComparer.Ordinal))
        {
            if (!visited.Add(nodeId))
            {
                continue;
            }

            var queue = new Queue<string>();
            var componentNodeIds = new List<string>();
            var degreeSum = 0;
            queue.Enqueue(nodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                componentNodeIds.Add(current);

                if (!adjacency.TryGetValue(current, out var neighbors))
                {
                    continue;
                }

                degreeSum += neighbors.Count;

                foreach (var neighbor in neighbors.Order(StringComparer.Ordinal))
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            componentNodeIds.Sort(StringComparer.Ordinal);
            components.Add(new GraphComponent(componentNodeIds, degreeSum / 2));
        }

        return components
            .OrderByDescending(component => component.NodeCount)
            .ThenByDescending(component => component.LinkCount)
            .ThenBy(component => component.NodeIds[0], StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> FindArticulationPoints(IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        var time = 0;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var discovery = new Dictionary<string, int>(StringComparer.Ordinal);
        var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var parent = new Dictionary<string, string?>(StringComparer.Ordinal);
        var articulationPoints = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nodeId in adjacency.Keys.Order(StringComparer.Ordinal))
        {
            if (!visited.Contains(nodeId))
            {
                DepthFirstSearch(
                    nodeId,
                    adjacency,
                    visited,
                    discovery,
                    low,
                    parent,
                    articulationPoints,
                    ref time);
            }
        }

        return articulationPoints;
    }

    private static void DepthFirstSearch(
        string nodeId,
        IReadOnlyDictionary<string, HashSet<string>> adjacency,
        HashSet<string> visited,
        Dictionary<string, int> discovery,
        Dictionary<string, int> low,
        Dictionary<string, string?> parent,
        HashSet<string> articulationPoints,
        ref int time)
    {
        visited.Add(nodeId);
        discovery[nodeId] = low[nodeId] = ++time;
        var childCount = 0;

        foreach (var neighbor in adjacency[nodeId].Order(StringComparer.Ordinal))
        {
            if (!visited.Contains(neighbor))
            {
                childCount++;
                parent[neighbor] = nodeId;
                DepthFirstSearch(neighbor, adjacency, visited, discovery, low, parent, articulationPoints, ref time);
                low[nodeId] = Math.Min(low[nodeId], low[neighbor]);

                var isRoot = !parent.ContainsKey(nodeId);
                if ((isRoot && childCount > 1) ||
                    (!isRoot && low[neighbor] >= discovery[nodeId]))
                {
                    articulationPoints.Add(nodeId);
                }
            }
            else if (!string.Equals(parent.GetValueOrDefault(nodeId), neighbor, StringComparison.Ordinal))
            {
                low[nodeId] = Math.Min(low[nodeId], discovery[neighbor]);
            }
        }
    }

    private static IReadOnlyCollection<CollectorTopologyLinkSummary> BuildStrongestLinks(
        IReadOnlyCollection<CollectorMapLinkSummary> currentLinks,
        IReadOnlyCollection<CollectorNeighborLinkHourlyRollup> rollups,
        IReadOnlyDictionary<string, MeshBoard.Contracts.Nodes.NodeSummary> nodeById,
        int topCount)
    {
        var rollupsByKey = rollups
            .GroupBy(rollup => CreateLinkKey(rollup.SourceNodeId, rollup.TargetNodeId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        return currentLinks
            .DistinctBy(link => CreateLinkKey(link.SourceNodeId, link.TargetNodeId), StringComparer.Ordinal)
            .Select(link =>
            {
                var linkKey = CreateLinkKey(link.SourceNodeId, link.TargetNodeId);
                var sourceNode = nodeById.GetValueOrDefault(link.SourceNodeId);
                var targetNode = nodeById.GetValueOrDefault(link.TargetNodeId);

                if (!rollupsByKey.TryGetValue(linkKey, out var matchingRollups) || matchingRollups.Length == 0)
                {
                    return link.ToCollectorTopologyLinkSummary(
                        sourceNode,
                        targetNode,
                        1,
                        link.SnrDb,
                        link.SnrDb,
                        link.SnrDb);
                }

                var totalObservationCount = matchingRollups.Sum(rollup => rollup.ObservationCount);
                var weightedSnrObservations = matchingRollups
                    .Where(rollup => rollup.AverageSnrDb.HasValue)
                    .Sum(rollup => rollup.ObservationCount);
                var weightedSnrSum = matchingRollups
                    .Where(rollup => rollup.AverageSnrDb.HasValue)
                    .Sum(rollup => rollup.AverageSnrDb!.Value * rollup.ObservationCount);
                var latestRollup = matchingRollups
                    .OrderByDescending(rollup => rollup.LastSeenAtUtc)
                    .ThenByDescending(rollup => rollup.ObservationCount)
                    .First();

                return link.ToCollectorTopologyLinkSummary(
                    sourceNode,
                    targetNode,
                    totalObservationCount,
                    weightedSnrObservations > 0 ? weightedSnrSum / weightedSnrObservations : link.SnrDb,
                    matchingRollups
                        .Where(rollup => rollup.MaxSnrDb.HasValue)
                        .Select(rollup => rollup.MaxSnrDb!.Value)
                        .DefaultIfEmpty(link.SnrDb ?? float.MinValue)
                        .Max() is var maxSnr && maxSnr != float.MinValue ? maxSnr : link.SnrDb,
                    latestRollup.LastSnrDb ?? link.SnrDb);
            })
            .OrderByDescending(link => link.ObservationCount)
            .ThenByDescending(link => link.AverageSnrDb ?? float.MinValue)
            .ThenByDescending(link => link.MaxSnrDb ?? float.MinValue)
            .ThenBy(link => link.SourceNodeId, StringComparer.Ordinal)
            .ThenBy(link => link.TargetNodeId, StringComparer.Ordinal)
            .Take(topCount)
            .ToArray();
    }

    private static string CreateLinkKey(string sourceNodeId, string targetNodeId)
    {
        return string.CompareOrdinal(sourceNodeId, targetNodeId) <= 0
            ? $"{sourceNodeId}|{targetNodeId}"
            : $"{targetNodeId}|{sourceNodeId}";
    }

    private sealed record GraphComponent(IReadOnlyList<string> NodeIds, int LinkCount)
    {
        public int NodeCount => NodeIds.Count;
    }

    private sealed record TopologyAnalysis(
        IReadOnlyDictionary<string, MeshBoard.Contracts.Nodes.NodeSummary> NodeById,
        IReadOnlyCollection<GraphComponent> Components,
        IReadOnlyDictionary<string, int> ComponentSizes,
        IReadOnlyDictionary<string, int> Degrees,
        IReadOnlyCollection<string> ArticulationNodeIds,
        int ConnectedComponentCount,
        int LargestConnectedComponentSize,
        int IsolatedNodeCount,
        int BridgeNodeCount,
        IReadOnlyCollection<CollectorTopologyLinkSummary> StrongestLinks);

    private sealed record OverviewChannelContext(
        CollectorChannelSummary Channel,
        IReadOnlyCollection<MeshBoard.Contracts.Nodes.NodeSummary> Nodes,
        CollectorOverviewChannelSummary Summary);
}
