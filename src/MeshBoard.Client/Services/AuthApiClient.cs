using System.Net.Http.Json;
using System.Net;
using MeshBoard.Client.Authentication;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Client.Services;

public sealed class AuthApiClient
{
    private readonly AntiforgeryTokenProvider _antiforgeryTokenProvider;
    private readonly AuthSessionState _authSessionState;
    private readonly HttpClient _httpClient;
    private readonly ApiRequestFactory _requestFactory;

    public AuthApiClient(
        HttpClient httpClient,
        ApiRequestFactory requestFactory,
        AntiforgeryTokenProvider antiforgeryTokenProvider,
        AuthSessionState authSessionState)
    {
        _httpClient = httpClient;
        _requestFactory = requestFactory;
        _antiforgeryTokenProvider = antiforgeryTokenProvider;
        _authSessionState = authSessionState;
    }

    public async Task<AuthenticatedUserResponse?> LoginAsync(
        LoginUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var antiforgeryToken = await _antiforgeryTokenProvider.GetAsync(cancellationToken: cancellationToken);

        using var httpRequest = _requestFactory.CreateJson(HttpMethod.Post, "/api/auth/login", request, antiforgeryToken);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await ApiRequestFactory.ReadErrorMessageAsync(response, "Login failed.", cancellationToken));
        }

        var user = await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty login payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var antiforgeryToken = await _antiforgeryTokenProvider.GetAsync(forceRefresh: true, cancellationToken);
            using var httpRequest = _requestFactory.Create(HttpMethod.Post, "/api/auth/logout", antiforgeryToken);
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        finally
        {
            _antiforgeryTokenProvider.Clear();
            _authSessionState.Clear();
        }
    }

    public async Task<AuthenticatedUserResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var antiforgeryToken = await _antiforgeryTokenProvider.GetAsync(cancellationToken: cancellationToken);

        using var httpRequest = _requestFactory.CreateJson(HttpMethod.Post, "/api/auth/register", request, antiforgeryToken);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                await ApiRequestFactory.ReadErrorMessageAsync(response, "Registration failed.", cancellationToken));
        }

        var user = await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty register payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }

    public async Task<AuthenticatedUserResponse?> TryLoadCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var request = _requestFactory.Create(HttpMethod.Get, "/api/auth/me");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _authSessionState.Clear();
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _authSessionState.Clear();
            return null;
        }

        var user = await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The API returned an empty current-user payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }
}
