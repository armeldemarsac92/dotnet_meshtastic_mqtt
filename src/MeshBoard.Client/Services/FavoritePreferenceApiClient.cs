using System.Net;
using MeshBoard.Api.SDK.API;
using MeshBoard.Contracts.Favorites;

namespace MeshBoard.Client.Services;

public sealed class FavoritePreferenceApiClient
{
    private readonly IFavoritePreferenceApi _favoritePreferenceApi;

    public FavoritePreferenceApiClient(
        IFavoritePreferenceApi favoritePreferenceApi)
    {
        _favoritePreferenceApi = favoritePreferenceApi;
    }

    public async Task<IReadOnlyList<FavoriteNode>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _favoritePreferenceApi.GetFavoritesAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading favorites failed."));
        }

        return response.Content ?? [];
    }

    public async Task<FavoriteNode> SaveFavoriteAsync(
        SaveFavoriteNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _favoritePreferenceApi.SaveFavoriteAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Saving the favorite failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty favorite payload.");
    }

    public async Task RemoveFavoriteAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var response = await _favoritePreferenceApi.RemoveFavoriteAsync(nodeId, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var fallbackMessage = response.StatusCode == HttpStatusCode.NotFound
                ? "Favorite not found."
                : "Removing the favorite failed.";

            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, fallbackMessage));
        }
    }
}
