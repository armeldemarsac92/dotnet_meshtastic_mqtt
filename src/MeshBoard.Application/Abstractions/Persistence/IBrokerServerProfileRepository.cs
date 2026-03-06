using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IBrokerServerProfileRepository
{
    Task<IReadOnlyCollection<BrokerServerProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<BrokerServerProfile?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<BrokerServerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task ClearActiveAsync(CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BrokerServerProfile> UpsertAsync(
        SaveBrokerServerProfileRequest request,
        CancellationToken cancellationToken = default);
}
