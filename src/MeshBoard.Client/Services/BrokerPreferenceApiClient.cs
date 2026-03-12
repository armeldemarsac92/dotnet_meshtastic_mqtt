using System.Net;
using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Client.Services;

public sealed class BrokerPreferenceApiClient
{
    private readonly IBrokerPreferenceApi _brokerPreferenceApi;

    public BrokerPreferenceApiClient(IBrokerPreferenceApi brokerPreferenceApi)
    {
        _brokerPreferenceApi = brokerPreferenceApi;
    }

    public async Task<SavedBrokerServerProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var response = await _brokerPreferenceApi.GetActiveAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading the active broker failed."));
        }

        return response.Content;
    }

    public async Task<IReadOnlyList<SavedBrokerServerProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await _brokerPreferenceApi.GetAllAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading broker profiles failed."));
        }

        return response.Content ?? [];
    }
}
