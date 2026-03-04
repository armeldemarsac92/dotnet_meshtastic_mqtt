using MeshBoard.Contracts.Nodes;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface INodeRepository
{
    Task<IReadOnlyCollection<NodeSummary>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(UpsertObservedNodeRequest request, CancellationToken cancellationToken = default);
}
