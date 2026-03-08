using System.Globalization;
using MeshBoard.Application.Authentication;
using MeshBoard.Infrastructure.Persistence.SQL.Responses;

namespace MeshBoard.Infrastructure.Persistence.Mapping;

internal static class UserAccountMapping
{
    public static UserAccountRecord MapToUserAccountRecord(this UserAccountSqlResponse response)
    {
        return new UserAccountRecord
        {
            Id = response.Id,
            Username = response.Username,
            NormalizedUsername = response.NormalizedUsername,
            PasswordHash = response.PasswordHash,
            CreatedAtUtc = DateTimeOffset.TryParse(
                response.CreatedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var createdAtUtc)
                ? createdAtUtc
                : DateTimeOffset.UtcNow
        };
    }
}
