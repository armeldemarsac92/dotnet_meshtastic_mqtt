using System.Security.Claims;
using MeshBoard.Contracts.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace MeshBoard.Client.Authentication;

public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly ClaimsPrincipal AnonymousPrincipal = new(new ClaimsIdentity());

    private readonly AuthSessionState _authSessionState;

    public ApiAuthenticationStateProvider(AuthSessionState authSessionState)
    {
        _authSessionState = authSessionState;
        _authSessionState.Changed += HandleSessionChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(BuildAuthenticationState());
    }

    public void Dispose()
    {
        _authSessionState.Changed -= HandleSessionChanged;
    }

    private AuthenticationState BuildAuthenticationState()
    {
        var user = _authSessionState.CurrentUser;
        if (user is null)
        {
            return new AuthenticationState(AnonymousPrincipal);
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(MeshBoardClaimTypes.WorkspaceId, user.WorkspaceId)
        ], "MeshBoard.Api.Cookie");

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private void HandleSessionChanged()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(BuildAuthenticationState()));
    }
}
