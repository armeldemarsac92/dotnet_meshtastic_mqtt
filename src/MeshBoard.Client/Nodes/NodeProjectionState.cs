namespace MeshBoard.Client.Nodes;

public sealed class NodeProjectionState
{
    public event Action? Changed;

    public NodeProjectionSnapshot Snapshot { get; private set; } = new();

    public void SetSnapshot(NodeProjectionSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
