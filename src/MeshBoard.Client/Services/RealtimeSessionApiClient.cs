using MeshBoard.Api.SDK.API;
using MeshBoard.Contracts.Realtime;

namespace MeshBoard.Client.Services;

public sealed class RealtimeSessionApiClient
{
    private readonly IRealtimeSessionApi _realtimeSessionApi;

    public RealtimeSessionApiClient(IRealtimeSessionApi realtimeSessionApi)
    {
        _realtimeSessionApi = realtimeSessionApi;
    }

    public async Task<RealtimeSessionResponse> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _realtimeSessionApi.CreateSessionAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Opening the realtime session failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty realtime session payload.");
    }
}
