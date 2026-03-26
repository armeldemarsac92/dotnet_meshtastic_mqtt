namespace MeshBoard.Client.Vault;

public sealed class VaultSessionState
{
    public event Action? Changed;

    public VaultStatusSnapshot Snapshot { get; private set; } = new();

    public void SetSnapshot(VaultStatusSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
