using MeshBoard.Contracts.Configuration;

namespace MeshBoard.Contracts.Meshtastic;

public static class BrokerRuntimeSnapshotMappingExtensions
{
    public static BrokerRuntimeSnapshot ToDefaultBrokerRuntimeSnapshot(this BrokerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new BrokerRuntimeSnapshot
        {
            ActiveServerName = "Default server",
            ActiveServerAddress = $"{options.Host}:{options.Port}",
            IsConnected = false,
            TopicFilters = []
        };
    }

    public static BrokerRuntimeSnapshot CloneSnapshot(this BrokerRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

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

    public static RuntimePipelineSnapshot CloneSnapshot(this RuntimePipelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

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
