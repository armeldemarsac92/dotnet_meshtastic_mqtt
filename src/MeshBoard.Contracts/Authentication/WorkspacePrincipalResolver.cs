using System.Security.Claims;

namespace MeshBoard.Contracts.Authentication;

public static class WorkspacePrincipalResolver
{
    public static string? ResolveWorkspaceId(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var workspaceId = principal.FindFirst(MeshBoardClaimTypes.WorkspaceId)?.Value;
        return string.IsNullOrWhiteSpace(workspaceId)
            ? null
            : workspaceId.Trim();
    }
}
