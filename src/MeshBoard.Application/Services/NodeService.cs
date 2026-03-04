using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Nodes;
using Microsoft.Extensions.Logging;

namespace MeshBoard.Application.Services;

public interface INodeService
{
    Task<IReadOnlyCollection<NodeSummary>> GetNodes(CancellationToken cancellationToken = default);
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

    public async Task<IReadOnlyCollection<NodeSummary>> GetNodes(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to get nodes");

        var nodes = await _nodeRepository.GetAllAsync(cancellationToken);

        _logger.LogInformation("Retrieved {NodeCount} nodes", nodes.Count);

        return nodes;
    }
}
