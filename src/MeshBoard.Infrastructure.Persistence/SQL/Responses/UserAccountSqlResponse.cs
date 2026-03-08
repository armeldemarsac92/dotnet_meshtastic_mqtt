namespace MeshBoard.Infrastructure.Persistence.SQL.Responses;

internal sealed class UserAccountSqlResponse
{
    public string CreatedAtUtc { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string NormalizedUsername { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
}
