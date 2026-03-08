using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Application.Authentication;

public sealed class UserAccountRecord
{
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string NormalizedUsername { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public AppUser ToAppUser()
    {
        return new AppUser
        {
            Id = Id,
            Username = Username,
            CreatedAtUtc = CreatedAtUtc
        };
    }
}
