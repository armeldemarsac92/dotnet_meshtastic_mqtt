using MeshBoard.Contracts.Topics;
using Refit;

namespace MeshBoard.Api.SDK.API;

public interface ITopicPresetPreferenceApi
{
    [Get("/api/preferences/topic-presets")]
    Task<IApiResponse<List<SavedTopicPreset>>> GetAllAsync(CancellationToken cancellationToken = default);
}
