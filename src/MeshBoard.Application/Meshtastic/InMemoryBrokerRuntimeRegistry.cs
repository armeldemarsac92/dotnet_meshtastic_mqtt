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
        var options = brokerOptions.Value;

        _defaultSnapshot = new BrokerRuntimeSnapshot
        {
            ActiveServerName = "Default server",
            ActiveServerAddress = $"{options.Host}:{options.Port}",
            IsConnected = false,
            TopicFilters = []
        };
    }

    public BrokerRuntimeSnapshot GetSnapshot(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        lock (_sync)
        {
            return _snapshotsByWorkspace.TryGetValue(workspaceId, out var snapshot)
                ? Clone(snapshot)
                : Clone(_defaultSnapshot);
        }
    }

    public void UpdateSnapshot(string workspaceId, BrokerRuntimeSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            _snapshotsByWorkspace[workspaceId] = Clone(snapshot);
        }
    }

    public RuntimePipelineSnapshot GetPipelineSnapshot(string workspaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);

        lock (_sync)
        {
            return _pipelineSnapshotsByWorkspace.TryGetValue(workspaceId, out var snapshot)
                ? Clone(snapshot)
                : new RuntimePipelineSnapshot();
        }
    }

    public void UpdatePipelineSnapshot(string workspaceId, RuntimePipelineSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            _pipelineSnapshotsByWorkspace[workspaceId] = Clone(snapshot);
        }
    }

    private static BrokerRuntimeSnapshot Clone(BrokerRuntimeSnapshot snapshot)
    {
        return new BrokerRuntimeSnapshot
        {
            ActiveServerProfileId = snapshot.ActiveServerProfileId,
            ActiveServerName = snapshot.ActiveServerName,
            ActiveServerAddress = snapshot.ActiveServerAddress,
            IsConnected = snapshot.IsConnected,
            LastStatusMessage = snapshot.LastStatusMessage,
            TopicFilters = [..snapshot.TopicFilters]
        };
    }

    private static RuntimePipelineSnapshot Clone(RuntimePipelineSnapshot snapshot)
    {
        return new RuntimePipelineSnapshot
        {
            InboundQueueCapacity = snapshot.InboundQueueCapacity,
            InboundWorkerCount = snapshot.InboundWorkerCount,
            InboundQueueDepth = snapshot.InboundQueueDepth,
            InboundOldestMessageAgeMilliseconds = snapshot.InboundOldestMessageAgeMilliseconds,
            InboundEnqueuedCount = snapshot.InboundEnqueuedCount,
            InboundDequeuedCount = snapshot.InboundDequeuedCount,
            InboundDroppedCount = snapshot.InboundDroppedCount,
            UpdatedAtUtc = snapshot.UpdatedAtUtc
        };
    }
}
