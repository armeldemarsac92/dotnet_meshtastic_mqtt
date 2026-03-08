using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IBrokerRuntimeCommandRepository
{
    Task EnqueueAsync(BrokerRuntimeCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BrokerRuntimeCommand>> GetRecentAsync(
        string workspaceId,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BrokerRuntimeCommand>> LeasePendingAsync(
        string processorId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(Guid commandId, CancellationToken cancellationToken = default);

    Task MarkPendingAsync(
        Guid commandId,
        DateTimeOffset availableAtUtc,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid commandId,
        string? lastError,
        CancellationToken cancellationToken = default);
}
