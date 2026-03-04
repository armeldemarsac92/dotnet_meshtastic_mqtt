using MeshBoard.Contracts.Messages;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IMessageRepository
{
    Task AddAsync(SaveObservedMessageRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MessageSummary>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
