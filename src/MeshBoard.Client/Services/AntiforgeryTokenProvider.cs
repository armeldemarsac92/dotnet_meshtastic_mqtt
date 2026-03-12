using System.Net.Http.Json;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Client.Services;

public sealed class AntiforgeryTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly ApiRequestFactory _requestFactory;

    private string? _cachedToken;

    public AntiforgeryTokenProvider(HttpClient httpClient, ApiRequestFactory requestFactory)
    {
        _httpClient = httpClient;
        _requestFactory = requestFactory;
    }

    public void Clear()
    {
        _cachedToken = null;
    }

    public async Task<string> GetAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && !string.IsNullOrWhiteSpace(_cachedToken))
        {
            return _cachedToken;
        }

        using var request = _requestFactory.Create(HttpMethod.Get, "/api/auth/antiforgery");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty antiforgery payload.");

        if (string.IsNullOrWhiteSpace(payload.RequestToken))
        {
            throw new InvalidOperationException("The API antiforgery payload did not contain a request token.");
        }

        _cachedToken = payload.RequestToken;
        return _cachedToken;
    }
}
