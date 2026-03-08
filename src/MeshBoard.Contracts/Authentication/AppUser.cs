namespace MeshBoard.Contracts.Authentication;

public sealed class AppUser
{
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
