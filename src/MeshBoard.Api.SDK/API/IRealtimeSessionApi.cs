using MeshBoard.Contracts.Api;
using MeshBoard.Contracts.Realtime;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IRealtimeSessionApi
{
    [Post(ApiRoutes.Realtime.CreateSession)]
    Task<IApiResponse<RealtimeSessionResponse>> CreateSessionAsync(CancellationToken cancellationToken = default);
}
