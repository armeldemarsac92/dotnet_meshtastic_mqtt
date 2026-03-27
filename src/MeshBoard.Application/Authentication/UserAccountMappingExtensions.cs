using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Application.Authentication;

public static class UserAccountMappingExtensions
{
    public static AppUser ToAppUser(this UserAccountRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new AppUser
        {
            Id = record.Id,
            Username = record.Username,
            CreatedAtUtc = record.CreatedAtUtc
        };
    }

    public static AppUser ToAppUser(this CreateUserAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AppUser
        {
            Id = request.Id,
            Username = request.Username,
            CreatedAtUtc = request.CreatedAtUtc
        };
    }

    public static CreateUserAccountRequest ToCreateUserAccountRequest(
        this RegisterUserRequest request,
        string username,
        string normalizedUsername,
        string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateUserAccountRequest
        {
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordHash = passwordHash
        };
    }
}
