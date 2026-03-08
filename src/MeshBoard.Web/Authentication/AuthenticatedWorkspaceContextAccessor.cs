using System.Security.Claims;
using MeshBoard.Application.Abstractions.Workspaces;
using MeshBoard.Contracts.Authentication;
using MeshBoard.Contracts.Workspaces;
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
        return ResolveWorkspaceId(_httpContextAccessor.HttpContext?.User) ??
            ResolveWorkspaceId(TryGetAuthenticationStateUser()) ??
            WorkspaceConstants.DefaultWorkspaceId;
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

    private static string? ResolveWorkspaceId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirstValue(MeshBoardClaimTypes.WorkspaceId) ??
            user.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
