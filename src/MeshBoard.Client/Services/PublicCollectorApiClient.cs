using MeshBoard.Api.SDK.API;
using MeshBoard.Contracts.Collector;

namespace MeshBoard.Client.Services;

public sealed class PublicCollectorApiClient
{
    private readonly IPublicCollectorApi _publicCollectorApi;

    public PublicCollectorApiClient(IPublicCollectorApi publicCollectorApi)
    {
        _publicCollectorApi = publicCollectorApi;
    }

    public async Task<IReadOnlyCollection<CollectorServerSummary>> GetObservedServersAsync(CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetObservedServersAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector servers failed."));
        }

        return response.Content ?? [];
    }

    public async Task<IReadOnlyCollection<CollectorChannelSummary>> GetObservedChannelsAsync(
        CollectorMapQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetObservedChannelsAsync(query ?? new CollectorMapQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector channels failed."));
        }

        return response.Content ?? [];
    }

    public async Task<CollectorMapSnapshot> GetSnapshotAsync(
        CollectorMapQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetSnapshotAsync(query ?? new CollectorMapQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading the public collector map snapshot failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty public collector snapshot payload.");
    }

    public async Task<CollectorChannelPacketStatsSnapshot> GetChannelPacketStatsAsync(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetChannelPacketStatsAsync(query ?? new CollectorPacketStatsQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector channel packet stats failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty collector channel packet stats payload.");
    }

    public async Task<CollectorNodePacketStatsSnapshot> GetNodePacketStatsAsync(
        CollectorPacketStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetNodePacketStatsAsync(query ?? new CollectorPacketStatsQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector node packet stats failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty collector node packet stats payload.");
    }

    public async Task<CollectorNeighborLinkStatsSnapshot> GetNeighborLinkStatsAsync(
        CollectorNeighborLinkStatsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetNeighborLinkStatsAsync(query ?? new CollectorNeighborLinkStatsQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector neighbor-link stats failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty collector neighbor-link stats payload.");
    }

    public async Task<CollectorTopologySnapshot> GetTopologyAsync(
        CollectorTopologyQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetTopologyAsync(query ?? new CollectorTopologyQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector topology failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty collector topology payload.");
    }

    public async Task<CollectorOverviewSnapshot> GetOverviewAsync(
        CollectorOverviewQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetOverviewAsync(query ?? new CollectorOverviewQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading the public collector overview failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty collector overview payload.");
    }

    public async Task<CollectorNodePage> GetNodePageAsync(
        CollectorNodePageQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetNodePageAsync(query ?? new CollectorNodePageQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector nodes failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty collector node page payload.");
    }

    public async Task<CollectorChannelPage> GetChannelPageAsync(
        CollectorChannelPageQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _publicCollectorApi.GetChannelPageAsync(query ?? new CollectorChannelPageQuery(), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                ApiProblemDetailsParser.GetMessage(response, "Loading public collector channels failed."));
        }

        return response.Content
            ?? throw new InvalidOperationException("The API returned an empty collector channel page payload.");
    }
}
