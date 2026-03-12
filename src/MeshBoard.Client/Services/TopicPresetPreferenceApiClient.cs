using System.Net;
using MeshBoard.Api.SDK.API;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.Client.Services;

public sealed class TopicPresetPreferenceApiClient
{
    private readonly ITopicPresetPreferenceApi _topicPresetPreferenceApi;

    public TopicPresetPreferenceApiClient(ITopicPresetPreferenceApi topicPresetPreferenceApi)
    {
        _topicPresetPreferenceApi = topicPresetPreferenceApi;
    }

    public async Task<IReadOnlyList<SavedTopicPreset>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await _topicPresetPreferenceApi.GetAllAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading topic presets failed."));
        }

        return response.Content ?? [];
    }

    public async Task<SavedTopicPreset> SaveAsync(
        SaveTopicPresetPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _topicPresetPreferenceApi.SaveAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Saving the topic preset failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty topic preset payload.");
    }
}
