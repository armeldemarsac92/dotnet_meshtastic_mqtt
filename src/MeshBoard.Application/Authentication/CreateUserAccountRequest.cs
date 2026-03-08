namespace MeshBoard.Application.Authentication;

public sealed class CreateUserAccountRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Username { get; set; } = string.Empty;

    public string NormalizedUsername { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
