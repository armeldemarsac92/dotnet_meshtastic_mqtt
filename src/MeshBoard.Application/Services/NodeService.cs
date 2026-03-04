using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface INodeService
{
    Task<IReadOnlyCollection<NodeSummary>> GetNodes(
        NodeQuery? query = null,
        int take = 100,
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

    private readonly ILogger<NodeService> _logger;
    private readonly INodeRepository _nodeRepository;

    public NodeService(INodeRepository nodeRepository, ILogger<NodeService> logger)
    {
        _nodeRepository = nodeRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<NodeSummary>> GetNodes(
        NodeQuery? query = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to get nodes with take: {Take}", take);

        var sanitizedTake = SanitizeTake(take);
        var sanitizedQuery = query ?? new NodeQuery();
        var nodes = await _nodeRepository.GetPageAsync(sanitizedQuery, 0, sanitizedTake, cancellationToken);

        _logger.LogInformation("Retrieved {NodeCount} nodes", nodes.Count);

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

        _logger.LogInformation(
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
}
