namespace MeshBoard.Contracts.CollectorEvents.Normalized;

public sealed class NodeObserved : CollectorEventMetadata
{
    public string TopicPattern { get; init; } = string.Empty;

    public string NodeId { get; init; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; init; }

    public string? ShortName { get; init; }

    public string? LongName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? LastHeardChannel { get; init; }

    public bool IsTextMessage { get; init; }
}
