using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Authentication;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IAntiforgeryApi
{
    [Get(ApiRoutes.Auth.GetAntiforgery)]
    Task<IApiResponse<AntiforgeryTokenResponse>> GetAntiforgeryTokenAsync(CancellationToken cancellationToken = default);
}
