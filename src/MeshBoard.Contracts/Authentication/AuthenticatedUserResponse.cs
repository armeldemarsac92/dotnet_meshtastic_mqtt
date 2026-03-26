namespace MeshBoard.Contracts.Authentication;

public sealed class AuthenticatedUserResponse
{
    public required string Id { get; set; }

    public required string Username { get; set; }

    public required string WorkspaceId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
