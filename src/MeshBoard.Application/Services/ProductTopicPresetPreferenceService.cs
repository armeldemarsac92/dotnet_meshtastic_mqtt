using MeshBoard.Contracts.Topics;

namespace MeshBoard.Application.Services;

public interface IProductTopicPresetPreferenceService
{
    Task<IReadOnlyCollection<SavedTopicPreset>> GetTopicPresetPreferences(CancellationToken cancellationToken = default);

    Task<SavedTopicPreset> SaveTopicPresetPreference(
        SaveTopicPresetPreferenceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProductTopicPresetPreferenceService : IProductTopicPresetPreferenceService
{
    private readonly ITopicPresetService _topicPresetService;

    public ProductTopicPresetPreferenceService(ITopicPresetService topicPresetService)
    {
        _topicPresetService = topicPresetService;
    }

    public async Task<IReadOnlyCollection<SavedTopicPreset>> GetTopicPresetPreferences(
        CancellationToken cancellationToken = default)
    {
        var presets = await _topicPresetService.GetTopicPresets(cancellationToken);
        return presets.Select(preset => preset.ToSavedTopicPreset()).ToList();
    }

    public async Task<SavedTopicPreset> SaveTopicPresetPreference(
        SaveTopicPresetPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var preset = await _topicPresetService.SaveTopicPreset(
            request.ToSaveTopicPresetRequest(),
            cancellationToken);

        return preset.ToSavedTopicPreset();
    }
}
