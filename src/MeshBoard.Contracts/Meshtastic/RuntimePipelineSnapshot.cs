namespace MeshBoard.Contracts.Meshtastic;

public sealed class RuntimePipelineSnapshot
{
    public int InboundQueueCapacity { get; set; }

    public int InboundWorkerCount { get; set; }

    public long InboundQueueDepth { get; set; }

    public long InboundOldestMessageAgeMilliseconds { get; set; }

    public long InboundEnqueuedCount { get; set; }

    public long InboundDequeuedCount { get; set; }

    public long InboundDroppedCount { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
