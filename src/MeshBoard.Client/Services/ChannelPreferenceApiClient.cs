using MeshBoard.Api.SDK.API;
using MeshBoard.Contracts.Topics;

namespace MeshBoard.Client.Services;

public sealed class ChannelPreferenceApiClient
{
    private readonly IChannelPreferenceApi _channelPreferenceApi;

    public ChannelPreferenceApiClient(IChannelPreferenceApi channelPreferenceApi)
    {
        _channelPreferenceApi = channelPreferenceApi;
    }

    public async Task<IReadOnlyList<SavedChannelFilter>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await _channelPreferenceApi.GetAllAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading saved channels failed."));
        }

        return response.Content ?? [];
    }

    public async Task SaveAsync(SaveChannelFilterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _channelPreferenceApi.SaveAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Saving the channel filter failed."));
        }
    }

    public async Task DeleteAsync(string topicFilter, CancellationToken cancellationToken = default)
    {
        var response = await _channelPreferenceApi.DeleteAsync(topicFilter, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Removing the channel filter failed."));
        }
    }
}
