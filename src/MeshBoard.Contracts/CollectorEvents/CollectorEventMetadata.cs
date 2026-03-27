namespace MeshBoard.Contracts.CollectorEvents;

public abstract class CollectorEventMetadata
{
    public Guid EventId { get; init; }

    public int SchemaVersion { get; init; } = CollectorEventSchemaVersions.V1;

    public string BrokerServer { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string? TraceParent { get; init; }
}
