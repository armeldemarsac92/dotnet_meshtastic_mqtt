using MeshBoard.Contracts.Favorites;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IFavoriteNodeRepository
{
    Task<IReadOnlyCollection<FavoriteNode>> GetAllAsync(
        string workspaceId,
        CancellationToken cancellationToken = default);

    Task<FavoriteNode> UpsertAsync(
        string workspaceId,
        SaveFavoriteNodeRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string workspaceId,
        string nodeId,
        CancellationToken cancellationToken = default);
}
