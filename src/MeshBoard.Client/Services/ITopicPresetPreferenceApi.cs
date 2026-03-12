using MeshBoard.Contracts.Topics;
using Refit;

namespace MeshBoard.Client.Services;

public interface ITopicPresetPreferenceApi
{
    [Get("/api/preferences/topic-presets")]
    Task<IApiResponse<List<SavedTopicPreset>>> GetAllAsync(CancellationToken cancellationToken = default);
}
