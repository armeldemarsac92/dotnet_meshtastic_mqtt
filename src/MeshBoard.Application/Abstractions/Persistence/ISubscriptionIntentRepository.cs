using MeshBoard.Contracts.Meshtastic;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface ISubscriptionIntentRepository
{
    Task<IReadOnlyCollection<SubscriptionIntent>> GetAllAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        CancellationToken cancellationToken = default);

    Task<bool> AddAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicFilter,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicFilter,
        CancellationToken cancellationToken = default);
}
