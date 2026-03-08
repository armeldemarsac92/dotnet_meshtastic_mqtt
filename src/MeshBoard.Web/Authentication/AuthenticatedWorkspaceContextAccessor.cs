using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace MeshBoard.Web.Authentication;

internal sealed class AuthenticatedWorkspaceContextAccessor : IWorkspaceContextAccessor
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public AuthenticatedWorkspaceContextAccessor(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public string GetWorkspaceId()
    {
        return WorkspacePrincipalResolver.ResolveWorkspaceId(TryGetAuthenticationStateUser()) ??
            throw new InvalidOperationException("No authenticated workspace context is available for the current request.");
    }

    private ClaimsPrincipal? TryGetAuthenticationStateUser()
    {
        try
        {
            return _authenticationStateProvider
                .GetAuthenticationStateAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult()
                .User;
        }
        catch
        {
            return null;
        }
    }

}
