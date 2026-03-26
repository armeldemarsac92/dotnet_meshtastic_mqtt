using MeshBoard.Contracts.Realtime;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IRealtimeSessionApi
{
    [Post("/api/realtime/session")]
    Task<IApiResponse<RealtimeSessionResponse>> CreateSessionAsync(CancellationToken cancellationToken = default);
}
