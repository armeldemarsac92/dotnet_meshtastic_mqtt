using MeshBoard.Api.SDK.Abstractions;
using MeshBoard.Api.SDK.API;

namespace MeshBoard.RealtimeLoadTests.Api;

internal sealed class LoadTestAntiforgeryTokenProvider : IAntiforgeryRequestTokenProvider
{
    private readonly IAntiforgeryApi _antiforgeryApi;
    private string? _cachedToken;

    public LoadTestAntiforgeryTokenProvider(IAntiforgeryApi antiforgeryApi)
    {
        _antiforgeryApi = antiforgeryApi;
    }

    public async Task<string> GetAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && !string.IsNullOrWhiteSpace(_cachedToken))
        {
            return _cachedToken;
        }

        var response = await _antiforgeryApi.GetAntiforgeryTokenAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                LoadTestApiProblemDetailsParser.GetMessage(response, "Loading the antiforgery token failed."));
        }

        var payload = response.Content
            ?? throw new InvalidOperationException("The API returned an empty antiforgery payload.");

        if (string.IsNullOrWhiteSpace(payload.RequestToken))
        {
            throw new InvalidOperationException("The API antiforgery payload did not contain a request token.");
        }

        _cachedToken = payload.RequestToken;
        return _cachedToken;
    }
}
