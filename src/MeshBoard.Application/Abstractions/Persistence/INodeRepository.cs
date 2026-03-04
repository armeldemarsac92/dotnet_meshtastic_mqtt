using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface INodeRepository
{
    Task<int> CountAsync(NodeQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NodeSummary>> GetPageAsync(
        NodeQuery query,
        int offset,
        int take,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default);
}
