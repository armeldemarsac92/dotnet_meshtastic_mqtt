namespace MeshBoard.Contracts.CollectorEvents.DeadLetter;

public sealed class DeadLetterEvent : CollectorEventMetadata
{
    public string OriginalTopic { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public DateTimeOffset OriginalReceivedAtUtc { get; init; }
}
