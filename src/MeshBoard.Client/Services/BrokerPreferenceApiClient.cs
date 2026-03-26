using System.Net;
using MeshBoard.Api.SDK.API;
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
                ApiProblemDetailsParser.GetMessage(response, "Loading the active server failed."));
        }

        return response.Content;
    }

    public async Task<IReadOnlyList<SavedBrokerServerProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await _brokerPreferenceApi.GetAllAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading server profiles failed."));
        }

        return response.Content ?? [];
    }

    public async Task<SavedBrokerServerProfile> CreateAsync(
        SaveBrokerPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _brokerPreferenceApi.CreateAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Saving the server profile failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty server profile payload.");
    }

    public async Task<SavedBrokerServerProfile> UpdateAsync(
        Guid id,
        SaveBrokerPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _brokerPreferenceApi.UpdateAsync(id, request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Updating the server profile failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty server profile payload.");
    }

    public async Task<SavedBrokerServerProfile> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _brokerPreferenceApi.ActivateAsync(id, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Activating the server profile failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty server profile payload.");
    }
}
