using System.Net.Http.Json;
using MeshBoard.Contracts.Favorites;

namespace MeshBoard.Client.Services;

public sealed class FavoritePreferenceApiClient
{
    private readonly AntiforgeryTokenProvider _antiforgeryTokenProvider;
    private readonly HttpClient _httpClient;
    private readonly ApiRequestFactory _requestFactory;

    public FavoritePreferenceApiClient(
        HttpClient httpClient,
        ApiRequestFactory requestFactory,
        AntiforgeryTokenProvider antiforgeryTokenProvider)
    {
        _httpClient = httpClient;
        _requestFactory = requestFactory;
        _antiforgeryTokenProvider = antiforgeryTokenProvider;
    }

    public async Task<IReadOnlyList<FavoriteNode>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        using var request = _requestFactory.Create(HttpMethod.Get, "/api/preferences/favorites");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<FavoriteNode>>(cancellationToken) ?? [];
    }

    public async Task<FavoriteNode> SaveFavoriteAsync(
        SaveFavoriteNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var antiforgeryToken = await _antiforgeryTokenProvider.GetAsync(cancellationToken: cancellationToken);

        using var httpRequest = _requestFactory.CreateJson(
            HttpMethod.Post,
            "/api/preferences/favorites",
            request,
            antiforgeryToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await ApiRequestFactory.ReadErrorMessageAsync(response, "Saving the favorite failed.", cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<FavoriteNode>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty favorite payload.");
    }

    public async Task RemoveFavoriteAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var antiforgeryToken = await _antiforgeryTokenProvider.GetAsync(cancellationToken: cancellationToken);

        using var request = _requestFactory.Create(
            HttpMethod.Delete,
            $"/api/preferences/favorites/{Uri.EscapeDataString(nodeId)}",
            antiforgeryToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await ApiRequestFactory.ReadErrorMessageAsync(response, "Removing the favorite failed.", cancellationToken));
        }
    }
}
