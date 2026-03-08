using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface INodeService
{
    Task<int> CountNodes(
        NodeQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<NodeSummary?> GetNodeById(
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NodeSummary>> GetNodes(
        NodeQuery? query = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NodeSummary>> GetLocatedNodes(
        string? searchText = null,
        int take = 5_000,
        CancellationToken cancellationToken = default);

    Task<NodePageResult> GetNodesPage(
        NodeQuery? query = null,
        int offset = 0,
        int take = 100,
        CancellationToken cancellationToken = default);
}

public sealed class NodeService : INodeService
{
    private const int MaxTake = 1_000;
    private const int MaxLocatedTake = 10_000;

    private readonly ILogger<NodeService> _logger;
    private readonly INodeRepository _nodeRepository;

    public NodeService(INodeRepository nodeRepository, ILogger<NodeService> logger)
    {
        _nodeRepository = nodeRepository;
        _logger = logger;
    }

    public Task<int> CountNodes(
        NodeQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query ?? new NodeQuery();
        _logger.LogDebug("Attempting to count nodes");
        return _nodeRepository.CountAsync(sanitizedQuery, cancellationToken);
    }

    public async Task<NodeSummary?> GetNodeById(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        var normalizedNodeId = nodeId.Trim();

        _logger.LogDebug("Attempting to get node by id {NodeId}", normalizedNodeId);

        return await _nodeRepository.GetByIdAsync(normalizedNodeId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetNodes(
        NodeQuery? query = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Attempting to get nodes with take: {Take}", take);

        var sanitizedTake = SanitizeTake(take);
        var sanitizedQuery = query ?? new NodeQuery();
        var nodes = await _nodeRepository.GetPageAsync(sanitizedQuery, 0, sanitizedTake, cancellationToken);

        _logger.LogDebug("Retrieved {NodeCount} nodes", nodes.Count);

        return nodes;
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetLocatedNodes(
        string? searchText = null,
        int take = 5_000,
        CancellationToken cancellationToken = default)
    {
        var sanitizedTake = SanitizeLocatedTake(take);

        _logger.LogDebug(
            "Attempting to get located nodes with take: {Take} and search text present: {HasSearchText}",
            sanitizedTake,
            !string.IsNullOrWhiteSpace(searchText));

        var nodes = await _nodeRepository.GetLocatedAsync(searchText, sanitizedTake, cancellationToken);

        _logger.LogDebug("Retrieved {NodeCount} located nodes", nodes.Count);

        return nodes;
    }

    public async Task<NodePageResult> GetNodesPage(
        NodeQuery? query = null,
        int offset = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var sanitizedQuery = query ?? new NodeQuery();
        var sanitizedOffset = Math.Max(0, offset);
        var sanitizedTake = SanitizeTake(take);

        _logger.LogDebug(
            "Attempting to get node page with offset: {Offset}, take: {Take}",
            sanitizedOffset,
            sanitizedTake);

        var totalCountTask = _nodeRepository.CountAsync(sanitizedQuery, cancellationToken);
        var itemsTask = _nodeRepository.GetPageAsync(sanitizedQuery, sanitizedOffset, sanitizedTake, cancellationToken);

        await Task.WhenAll(totalCountTask, itemsTask);

        return new NodePageResult
        {
            TotalCount = await totalCountTask,
            Items = await itemsTask
        };
    }

    private static int SanitizeTake(int take)
    {
        if (take <= 0)
        {
            return 1;
        }

        return Math.Min(take, MaxTake);
    }

    private static int SanitizeLocatedTake(int take)
    {
        if (take <= 0)
        {
            return 1;
        }

        return Math.Min(take, MaxLocatedTake);
    }
}
