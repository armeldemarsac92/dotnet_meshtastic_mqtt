using MeshBoard.Contracts.Authentication;
using Refit;

namespace MeshBoard.Client.Services;

public interface IAuthApi
{
    [Post("/api/auth/login")]
    Task<IApiResponse<AuthenticatedUserResponse>> LoginAsync([Body] LoginUserRequest request, CancellationToken cancellationToken = default);

    [Post("/api/auth/register")]
    Task<IApiResponse<AuthenticatedUserResponse>> RegisterAsync([Body] RegisterUserRequest request, CancellationToken cancellationToken = default);

    [Get("/api/auth/me")]
    Task<IApiResponse<AuthenticatedUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    [Post("/api/auth/logout")]
    Task<IApiResponse> LogoutAsync(CancellationToken cancellationToken = default);
}
