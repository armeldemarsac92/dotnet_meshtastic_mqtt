using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface INeighborLinkRepository
{
    Task UpsertAsync(
        string brokerServer,
        string? channelKey,
        IReadOnlyList<NeighborLinkRecord> links,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NeighborLinkRecord>> GetActiveLinksAsync(
        DateTimeOffset notBeforeUtc,
        CancellationToken cancellationToken = default);
}
