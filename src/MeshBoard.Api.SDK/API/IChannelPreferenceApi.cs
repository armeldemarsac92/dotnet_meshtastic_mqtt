using MeshBoard.Contracts.Topics;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface IChannelPreferenceApi
{
    [Get("/api/preferences/channels")]
    Task<IApiResponse<List<SavedChannelFilter>>> GetAllAsync(CancellationToken cancellationToken = default);

    [Post("/api/preferences/channels")]
    Task<IApiResponse> SaveAsync([Body] SaveChannelFilterRequest request, CancellationToken cancellationToken = default);

    [Delete("/api/preferences/channels")]
    Task<IApiResponse> DeleteAsync([AliasAs("topicFilter")] string topicFilter, CancellationToken cancellationToken = default);
}
