using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Authentication;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IAuthApi
{
    [Post(ApiRoutes.Auth.Group + ApiRoutes.Auth.Login)]
    Task<IApiResponse<AuthenticatedUserResponse>> LoginAsync([Body] LoginUserRequest request, CancellationToken cancellationToken = default);

    [Post(ApiRoutes.Auth.Group + ApiRoutes.Auth.Register)]
    Task<IApiResponse<AuthenticatedUserResponse>> RegisterAsync([Body] RegisterUserRequest request, CancellationToken cancellationToken = default);

    [Get(ApiRoutes.Auth.Group + ApiRoutes.Auth.Me)]
    Task<IApiResponse<AuthenticatedUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    [Post(ApiRoutes.Auth.Group + ApiRoutes.Auth.Logout)]
    Task<IApiResponse> LogoutAsync(CancellationToken cancellationToken = default);
}
