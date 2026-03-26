using MeshBoard.Contracts.Collector;

namespace MeshBoard.Client.Collector;

public sealed class PublicCollectorState
{
    public event Action? Changed;

    public CollectorOverviewSnapshot? Overview { get; private set; }

    public CollectorMapSnapshot? MapSnapshot { get; private set; }

    public CollectorTopologySnapshot? Topology { get; private set; }

    public bool IsLoading { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void SetLoading(bool isLoading)
    {
        IsLoading = isLoading;
        Changed?.Invoke();
    }

    public void SetError(string? errorMessage)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? null
            : errorMessage.Trim();
        Changed?.Invoke();
    }

    public void SetOverview(CollectorOverviewSnapshot? overview)
    {
        Overview = overview;
        Changed?.Invoke();
    }

    public void SetMapSnapshot(CollectorMapSnapshot? mapSnapshot)
    {
        MapSnapshot = mapSnapshot;
        Changed?.Invoke();
    }

    public void SetTopology(CollectorTopologySnapshot? topology)
    {
        Topology = topology;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Overview = null;
        MapSnapshot = null;
        Topology = null;
        IsLoading = false;
        ErrorMessage = null;
        Changed?.Invoke();
    }
}
