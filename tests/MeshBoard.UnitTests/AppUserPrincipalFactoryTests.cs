using System.Security.Claims;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.UnitTests;

public sealed class AppUserPrincipalFactoryTests
{
    [Fact]
    public void CreatePrincipal_ShouldPopulateExpectedAuthClaims()
    {
        var user = new AppUser
        {
            Id = "user-alpha",
            Username = "alpha.user",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var principal = AppUserPrincipalFactory.CreatePrincipal(user, "Cookies");

        Assert.Equal("Cookies", principal.Identity?.AuthenticationType);
        Assert.Equal("user-alpha", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("alpha.user", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("user-alpha", principal.FindFirstValue(MeshBoardClaimTypes.WorkspaceId));
    }
}
