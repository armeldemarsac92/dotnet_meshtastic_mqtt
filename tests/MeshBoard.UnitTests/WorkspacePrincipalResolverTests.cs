using System.Security.Claims;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.UnitTests;

public sealed class WorkspacePrincipalResolverTests
{
    [Fact]
    public void ResolveWorkspaceId_ShouldReturnWorkspaceClaim_ForAuthenticatedPrincipal()
    {
        var principal = CreatePrincipal(
            new Claim(MeshBoardClaimTypes.WorkspaceId, "workspace-alpha"),
            new Claim(ClaimTypes.NameIdentifier, "user-alpha"));

        var workspaceId = WorkspacePrincipalResolver.ResolveWorkspaceId(principal);

        Assert.Equal("workspace-alpha", workspaceId);
    }

    [Fact]
    public void ResolveWorkspaceId_ShouldNotFallbackToNameIdentifier_WhenWorkspaceClaimIsMissing()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-alpha"));

        var workspaceId = WorkspacePrincipalResolver.ResolveWorkspaceId(principal);

        Assert.Null(workspaceId);
    }

    [Fact]
    public void ResolveWorkspaceId_ShouldReturnNull_ForAnonymousPrincipal()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var workspaceId = WorkspacePrincipalResolver.ResolveWorkspaceId(principal);

        Assert.Null(workspaceId);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                authenticationType: "Cookies"));
    }
}
