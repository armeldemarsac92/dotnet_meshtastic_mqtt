using MeshBoard.Contracts.Messages;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IMessageRepository
{
    Task<bool> AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
