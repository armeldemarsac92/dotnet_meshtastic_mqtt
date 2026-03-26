namespace MeshBoard.Client.Realtime;

public sealed class RealtimeClientState
{
    public event Action? Changed;

    public RealtimeClientSnapshot Snapshot { get; private set; } = new();

    public void SetSnapshot(RealtimeClientSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
