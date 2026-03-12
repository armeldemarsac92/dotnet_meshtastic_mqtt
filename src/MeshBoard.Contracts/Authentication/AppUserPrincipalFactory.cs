using System.Security.Claims;

namespace MeshBoard.Contracts.Authentication;

public static class AppUserPrincipalFactory
{
    public static ClaimsPrincipal CreatePrincipal(AppUser user, string authenticationType)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(MeshBoardClaimTypes.WorkspaceId, user.Id)
        };

        var identity = new ClaimsIdentity(claims, authenticationType);
        return new ClaimsPrincipal(identity);
    }
}
