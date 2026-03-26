using MeshBoard.Client.Services;
using MeshBoard.Contracts.Collector;

namespace MeshBoard.Client.Collector;

public sealed class PublicCollectorService
{
    private readonly PublicCollectorApiClient _publicCollectorApiClient;
    private readonly PublicCollectorState _state;

    public PublicCollectorService(
        PublicCollectorApiClient publicCollectorApiClient,
        PublicCollectorState state)
    {
        _publicCollectorApiClient = publicCollectorApiClient;
        _state = state;
    }

    public event Action? Changed
    {
        add => _state.Changed += value;
        remove => _state.Changed -= value;
    }

    public PublicCollectorState State => _state;

    public void Clear()
    {
        _state.Clear();
    }

    public async Task<CollectorOverviewSnapshot> LoadOverviewAsync(
        CollectorOverviewQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            () => _publicCollectorApiClient.GetOverviewAsync(query, cancellationToken),
            overview => _state.SetOverview(overview));
    }

    public async Task<CollectorMapSnapshot> LoadMapSnapshotAsync(
        CollectorMapQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            () => _publicCollectorApiClient.GetSnapshotAsync(query, cancellationToken),
            snapshot => _state.SetMapSnapshot(snapshot));
    }

    public async Task<CollectorTopologySnapshot> LoadTopologyAsync(
        CollectorTopologyQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            () => _publicCollectorApiClient.GetTopologyAsync(query, cancellationToken),
            topology => _state.SetTopology(topology));
    }

    private async Task<TPayload> ExecuteAsync<TPayload>(
        Func<Task<TPayload>> action,
        Action<TPayload> apply)
    {
        _state.SetLoading(true);
        _state.SetError(null);

        try
        {
            var payload = await action();
            apply(payload);
            return payload;
        }
        catch (Exception exception)
        {
            _state.SetError(exception.Message);
            throw;
        }
        finally
        {
            _state.SetLoading(false);
        }
    }
}
