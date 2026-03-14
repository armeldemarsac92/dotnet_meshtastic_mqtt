namespace MeshBoard.Client.Maps;

public sealed class MapProjectionState
{
    public event Action? Changed;

    public MapProjectionSnapshot Snapshot { get; private set; } = new();

    public void SetSnapshot(MapProjectionSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
