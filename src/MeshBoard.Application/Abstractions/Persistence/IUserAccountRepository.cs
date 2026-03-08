using MeshBoard.Application.Authentication;
using MeshBoard.Contracts.Authentication;

namespace MeshBoard.Application.Abstractions.Persistence;

public interface IUserAccountRepository
{
    Task<UserAccountRecord?> GetByIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<UserAccountRecord?> GetByNormalizedUsernameAsync(
        string normalizedUsername,
        CancellationToken cancellationToken = default);

    Task<AppUser> InsertAsync(
        CreateUserAccountRequest request,
        CancellationToken cancellationToken = default);
}
