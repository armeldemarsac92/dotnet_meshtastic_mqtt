namespace MeshBoard.Contracts.Authentication;

public static class AppUserMappingExtensions
{
    public static AuthenticatedUserResponse ToAuthenticatedUserResponse(
        this AppUser user,
        string workspaceId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new AuthenticatedUserResponse
        {
            Id = user.Id,
            Username = user.Username,
            WorkspaceId = workspaceId,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }
}
