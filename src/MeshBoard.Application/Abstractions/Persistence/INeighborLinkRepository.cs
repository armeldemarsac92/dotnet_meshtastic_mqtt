using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface INeighborLinkRepository
{
    Task UpsertAsync(
        string workspaceId,
        IReadOnlyList<NeighborLinkRecord> links,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NeighborLinkRecord>> GetActiveLinksAsync(
        string workspaceId,
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);
}
