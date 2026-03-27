namespace MeshBoard.Contracts.CollectorEvents.Normalized;

public sealed class TelemetryObserved : CollectorEventMetadata
{
    public string TopicPattern { get; init; } = string.Empty;

    public string NodeId { get; init; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; init; }

    public string MetricType { get; init; } = string.Empty;

    public double MetricValue { get; init; }
}
