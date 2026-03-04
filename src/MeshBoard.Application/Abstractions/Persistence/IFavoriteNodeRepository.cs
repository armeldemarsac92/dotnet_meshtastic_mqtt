using MeshBoard.Contracts.Favorites;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IFavoriteNodeRepository
{
    Task<IReadOnlyCollection<FavoriteNode>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FavoriteNode> UpsertAsync(SaveFavoriteNodeRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string nodeId, CancellationToken cancellationToken = default);
}
