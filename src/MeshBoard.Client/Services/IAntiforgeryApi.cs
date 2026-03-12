using MeshBoard.Contracts.Authentication;
using Refit;

namespace MeshBoard.Client.Services;

public interface IAntiforgeryApi
{
    [Get("/api/auth/antiforgery")]
    Task<IApiResponse<AntiforgeryTokenResponse>> GetAntiforgeryTokenAsync(CancellationToken cancellationToken = default);
}
