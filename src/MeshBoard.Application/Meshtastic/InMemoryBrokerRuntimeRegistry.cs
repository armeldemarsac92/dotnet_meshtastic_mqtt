using MeshBoard.Application.Abstractions.Meshtastic;
using MeshBoard.Contracts.Configuration;
using MeshBoard.Contracts.Meshtastic;
using Microsoft.Extensions.Options;

namespace MeshBoard.Application.Meshtastic;

internal sealed class InMemoryBrokerRuntimeRegistry : IBrokerRuntimeRegistry
{
    private readonly object _sync = new();
    private readonly BrokerRuntimeSnapshot _defaultSnapshot;
    private readonly Dictionary<string, RuntimePipelineSnapshot> _pipelineSnapshotsByWorkspace = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BrokerRuntimeSnapshot> _snapshotsByWorkspace = new(StringComparer.Ordinal);

    public InMemoryBrokerRuntimeRegistry(IOptions<BrokerOptions> brokerOptions)
    {
        _defaultSnapshot = brokerOptions.Value.ToDefaultBrokerRuntimeSnapshot();
    }

    public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        lock (_sync)
        {
            return _snapshotsByWorkspace.TryGetValue(workspaceId, out var snapshot)
                ? snapshot.CloneSnapshot()
                : _defaultSnapshot.CloneSnapshot();
        }
    }

    public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            _snapshotsByWorkspace[workspaceId] = snapshot.CloneSnapshot();
        }
    }

    public RuntimePipelineSnapshot GetPipelineSnapshot(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        lock (_sync)
        {
            return _pipelineSnapshotsByWorkspace.TryGetValue(workspaceId, out var snapshot)
                ? snapshot.CloneSnapshot()
                : new RuntimePipelineSnapshot();
        }
    }

    public void UpdatePipelineSnapshot(string workspaceId, RuntimePipelineSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            _pipelineSnapshotsByWorkspace[workspaceId] = snapshot.CloneSnapshot();
        }
    }
}
