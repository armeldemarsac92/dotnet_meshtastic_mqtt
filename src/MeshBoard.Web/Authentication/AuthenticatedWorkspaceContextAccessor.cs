using System.Security.Claims;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace MeshBoard.Web.Authentication;

internal sealed class AuthenticatedWorkspaceContextAccessor : IWorkspaceContextAccessor
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticatedWorkspaceContextAccessor(
        AuthenticationStateProvider authenticationStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetWorkspaceId()
    {
        return WorkspacePrincipalResolver.ResolveWorkspaceId(_httpContextAccessor.HttpContext?.User) ??
            WorkspacePrincipalResolver.ResolveWorkspaceId(TryGetAuthenticationStateUser()) ??
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
