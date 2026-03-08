namespace MeshBoard.Contracts.Meshtastic;

public sealed class BrokerStatus
{
    public Guid? ActiveServerProfileId { get; set; }

    public string? ActiveServerName { get; set; }

    public string? ActiveServerAddress { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool IsConnected { get; set; }

    public string? LastStatusMessage { get; set; }

    public List<string> TopicFilters { get; set; } = [];

    public int InboundQueueCapacity { get; set; }

    public int InboundWorkerCount { get; set; }

    public long InboundQueueDepth { get; set; }

    public long InboundOldestMessageAgeMilliseconds { get; set; }

    public long InboundEnqueuedCount { get; set; }

    public long InboundDequeuedCount { get; set; }

    public long InboundDroppedCount { get; set; }

    public DateTimeOffset? RuntimeMetricsUpdatedAtUtc { get; set; }
}
