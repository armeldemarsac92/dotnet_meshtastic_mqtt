using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IBrokerServerProfileRepository
{
    Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WorkspaceBrokerServerProfile>> GetAllActiveUserOwnedAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(
        string workspaceId,
        CancellationToken cancellationToken = default);

    Task<BrokerServerProfile?> GetActiveAsync(
        string workspaceId,
        CancellationToken cancellationToken = default);

    Task<BrokerServerProfile?> GetByIdAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task SetExclusiveActiveAsync(
        string workspaceId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> UpsertAsync(
        string workspaceId,
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default);
}
