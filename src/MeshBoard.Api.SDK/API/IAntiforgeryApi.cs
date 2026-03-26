using MeshBoard.Contracts.Authentication;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IAntiforgeryApi
{
    [Get("/api/auth/antiforgery")]
    Task<IApiResponse<AntiforgeryTokenResponse>> GetAntiforgeryTokenAsync(CancellationToken cancellationToken = default);
}
