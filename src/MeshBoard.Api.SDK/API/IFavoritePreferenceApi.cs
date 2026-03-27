using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Favorites;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IFavoritePreferenceApi
{
    [Get(ApiRoutes.Preferences.Favorites.GetAll)]
    Task<IApiResponse<List<FavoriteNode>>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    [Post(ApiRoutes.Preferences.Favorites.Save)]
    Task<IApiResponse<FavoriteNode>> SaveFavoriteAsync([Body] SaveFavoriteNodeRequest request, CancellationToken cancellationToken = default);

    [Delete(ApiRoutes.Preferences.Favorites.Remove)]
    Task<IApiResponse> RemoveFavoriteAsync(string nodeId, CancellationToken cancellationToken = default);
}
