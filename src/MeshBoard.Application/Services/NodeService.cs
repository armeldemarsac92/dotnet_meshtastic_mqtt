using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface INodeService
{
    Task<IReadOnlyCollection<NodeSummary>> GetNodes(NodeQuery? query = null, CancellationToken cancellationToken = default);
}

public sealed class NodeService : INodeService
{
    private readonly ILogger<NodeService> _logger;
    private readonly INodeRepository _nodeRepository;

    public NodeService(INodeRepository nodeRepository, ILogger<NodeService> logger)
    {
        _nodeRepository = nodeRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetNodes(
        NodeQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to get nodes");

        var nodes = await _nodeRepository.GetAllAsync(cancellationToken);
        var filteredNodes = ApplyQuery(nodes, query);

        _logger.LogInformation("Retrieved {NodeCount} nodes", filteredNodes.Count);

        return filteredNodes;
    }

    private static IReadOnlyCollection<NodeSummary> ApplyQuery(
        IReadOnlyCollection<NodeSummary> nodes,
        NodeQuery? query)
    {
        if (query is null)
        {
            return Sort(nodes, NodeSortOption.LastHeardDesc).ToList();
        }

        IEnumerable<NodeSummary> filteredNodes = nodes;

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            filteredNodes = filteredNodes.Where(
                node =>
                    node.NodeId.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (node.ShortName?.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (node.LongName?.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (query.OnlyWithLocation)
        {
            filteredNodes = filteredNodes.Where(node => node.LastKnownLatitude.HasValue && node.LastKnownLongitude.HasValue);
        }

        if (query.OnlyWithTelemetry)
        {
            filteredNodes = filteredNodes.Where(HasTelemetry);
        }

        return Sort(filteredNodes, query.SortBy).ToList();
    }

    private static bool HasTelemetry(NodeSummary node)
    {
        return node.BatteryLevelPercent.HasValue ||
            node.Voltage.HasValue ||
            node.ChannelUtilization.HasValue ||
            node.AirUtilTx.HasValue ||
            node.UptimeSeconds.HasValue ||
            node.TemperatureCelsius.HasValue ||
            node.RelativeHumidity.HasValue ||
            node.BarometricPressure.HasValue;
    }

    private static IOrderedEnumerable<NodeSummary> Sort(
        IEnumerable<NodeSummary> nodes,
        NodeSortOption sortBy)
    {
        return sortBy switch
        {
            NodeSortOption.NameAsc => nodes
                .OrderBy(node => node.LongName ?? node.ShortName ?? node.NodeId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase),
            NodeSortOption.BatteryDesc => nodes
                .OrderByDescending(node => node.BatteryLevelPercent ?? int.MinValue)
                .ThenBy(node => node.LongName ?? node.ShortName ?? node.NodeId, StringComparer.OrdinalIgnoreCase),
            _ => nodes
                .OrderByDescending(node => node.LastHeardAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(node => node.LongName ?? node.ShortName ?? node.NodeId, StringComparer.OrdinalIgnoreCase)
        };
    }
}
