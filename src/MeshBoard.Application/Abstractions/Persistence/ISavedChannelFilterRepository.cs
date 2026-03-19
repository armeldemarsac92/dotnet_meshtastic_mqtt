using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface ISavedChannelFilterRepository
{
    Task<IReadOnlyCollection<SavedChannelFilter>> GetAllAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        CancellationToken cancellationToken = default);

    Task<bool> UpsertAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicFilter,
        string? label,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string workspaceId,
        Guid brokerServerProfileId,
        string topicFilter,
        CancellationToken cancellationToken = default);
}
