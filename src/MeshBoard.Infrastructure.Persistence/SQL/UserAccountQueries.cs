namespace MeshBoard.Infrastructure.Persistence.SQL;

internal static class UserAccountQueries
{
    public static string GetById =>
        """
        SELECT
            id AS Id,
            username AS Username,
            normalized_username AS NormalizedUsername,
            password_hash AS PasswordHash,
            created_at_utc AS CreatedAtUtc
        FROM users
        WHERE id = @Id
        LIMIT 1;
        """;

    public static string GetByNormalizedUsername =>
        """
        SELECT
            id AS Id,
            username AS Username,
            normalized_username AS NormalizedUsername,
            password_hash AS PasswordHash,
            created_at_utc AS CreatedAtUtc
        FROM users
        WHERE normalized_username = @NormalizedUsername
        LIMIT 1;
        """;

    public static string Insert =>
        """
        INSERT INTO users (
            id,
            username,
            normalized_username,
            password_hash,
            created_at_utc
        )
        VALUES (
            @Id,
            @Username,
            @NormalizedUsername,
            @PasswordHash,
            @CreatedAtUtc
        );
        """;
}
