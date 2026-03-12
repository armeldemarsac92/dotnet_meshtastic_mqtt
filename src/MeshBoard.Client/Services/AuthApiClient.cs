using System.Net;
using MeshBoard.Api.SDK.API;
using MeshBoard.Client.Authentication;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Client.Services;

public sealed class AuthApiClient
{
    private readonly IAuthApi _authApi;
    private readonly AntiforgeryTokenProvider _antiforgeryTokenProvider;
    private readonly AuthSessionState _authSessionState;

    public AuthApiClient(
        IAuthApi authApi,
        AntiforgeryTokenProvider antiforgeryTokenProvider,
        AuthSessionState authSessionState)
    {
        _authApi = authApi;
        _antiforgeryTokenProvider = antiforgeryTokenProvider;
        _authSessionState = authSessionState;
    }

    public async Task<AuthenticatedUserResponse?> LoginAsync(
        LoginUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _authApi.LoginAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Login failed."));
        }

        var user = response.Content
            ?? throw new InvalidOperationException("The API returned an empty login payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _antiforgeryTokenProvider.GetAsync(forceRefresh: true, cancellationToken: cancellationToken);
            var response = await _authApi.LogoutAsync(cancellationToken);

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException(
                    ApiProblemDetailsParser.GetMessage(response, "Logout failed."));
            }
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
        var response = await _authApi.RegisterAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Registration failed."));
        }

        var user = response.Content
            ?? throw new InvalidOperationException("The API returned an empty register payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }

    public async Task<AuthenticatedUserResponse?> TryLoadCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await _authApi.GetCurrentUserAsync(cancellationToken);

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

        var user = response.Content
            ?? throw new InvalidOperationException("The API returned an empty current-user payload.");

        _authSessionState.SetCurrentUser(user);
        return user;
    }
}
