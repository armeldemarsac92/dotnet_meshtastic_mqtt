namespace MeshBoard.Client.Messages;

public sealed class DecryptedMessageState
{
    public event Action? Changed;

    public DecryptedMessageSnapshot Snapshot { get; private set; } = new();

    public void SetSnapshot(DecryptedMessageSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
