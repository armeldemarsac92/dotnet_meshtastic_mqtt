using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Favorites;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IFavoritePreferenceApi
{
    [Get(ApiRoutes.Preferences.Favorites.Group + ApiRoutes.Preferences.Favorites.Root)]
    Task<IApiResponse<List<FavoriteNode>>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    [Post(ApiRoutes.Preferences.Favorites.Group + ApiRoutes.Preferences.Favorites.Root)]
    Task<IApiResponse<FavoriteNode>> SaveFavoriteAsync([Body] SaveFavoriteNodeRequest request, CancellationToken cancellationToken = default);

    [Delete(ApiRoutes.Preferences.Favorites.Group + ApiRoutes.Preferences.Favorites.ByNodeId)]
    Task<IApiResponse> RemoveFavoriteAsync(string nodeId, CancellationToken cancellationToken = default);
}
