using MeshBoard.Application.Abstractions.Collector;
using MeshBoard.Application.Abstractions.Persistence;
using MeshBoard.Contracts.Collector;
using MeshBoard.Contracts.Nodes;
using MeshBoard.Contracts.Workspaces;

namespace MeshBoard.Application.Services;

public sealed class PostgresTopologyReadAdapter : ITopologyReadAdapter
{
    private readonly ICollectorReadRepository _repository;

    public PostgresTopologyReadAdapter(ICollectorReadRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyCollection<NodeSummary>> GetTopologyNodesAsync(
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetTopologyNodesAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            query,
            notBeforeUtc,
            cancellationToken);
    }

    public Task<IReadOnlyCollection<CollectorMapLinkSummary>> GetTopologyLinksAsync(
        CollectorTopologyQuery query,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetTopologyLinksAsync(
            WorkspaceConstants.DefaultWorkspaceId,
            query,
            notBeforeUtc,
            cancellationToken);
    }
}
