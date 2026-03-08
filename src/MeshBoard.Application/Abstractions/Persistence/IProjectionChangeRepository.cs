using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IProjectionChangeRepository
{
    Task AppendAsync(
        string workspaceId,
        IReadOnlyCollection<ProjectionChangeDescriptor> changes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProjectionChangeEvent>> GetChangesAfterAsync(
        long lastSeenId,
        int take,
        CancellationToken cancellationToken = default);

    Task<long> GetLatestIdAsync(CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}
