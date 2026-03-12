using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Client.Services;

public sealed class AntiforgeryTokenProvider
{
    private readonly IAntiforgeryApi _antiforgeryApi;

    private string? _cachedToken;

    public AntiforgeryTokenProvider(IAntiforgeryApi antiforgeryApi)
    {
        _antiforgeryApi = antiforgeryApi;
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

        var response = await _antiforgeryApi.GetAntiforgeryTokenAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading the antiforgery token failed."));
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
