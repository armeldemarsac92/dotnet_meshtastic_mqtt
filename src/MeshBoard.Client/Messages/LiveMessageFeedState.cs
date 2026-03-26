namespace MeshBoard.Client.Messages;

public sealed class LiveMessageFeedState
{
    public event Action? Changed;

    public LiveMessageFeedSnapshot Snapshot { get; private set; } = new();

    public void SetSnapshot(LiveMessageFeedSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
