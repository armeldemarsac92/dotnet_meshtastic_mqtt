using MeshBoard.Contracts.Favorites;
using Refit;

namespace MeshBoard.Client.Services;

public interface IFavoritePreferenceApi
{
    [Get("/api/preferences/favorites")]
    Task<IApiResponse<List<FavoriteNode>>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    [Post("/api/preferences/favorites")]
    Task<IApiResponse<FavoriteNode>> SaveFavoriteAsync([Body] SaveFavoriteNodeRequest request, CancellationToken cancellationToken = default);

    [Delete("/api/preferences/favorites/{nodeId}")]
    Task<IApiResponse> RemoveFavoriteAsync(string nodeId, CancellationToken cancellationToken = default);
}
