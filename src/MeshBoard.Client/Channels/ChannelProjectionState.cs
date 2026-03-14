namespace MeshBoard.Client.Channels;

public sealed class ChannelProjectionState
{
    public event Action? Changed;

    public ChannelProjectionSnapshot Snapshot { get; private set; } = new();

    public void SetSnapshot(ChannelProjectionSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
