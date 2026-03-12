namespace MeshBoard.Client.Messages;

public sealed record LiveMessageFeedSnapshot
{
    public static readonly IReadOnlyList<LiveMessageEnvelope> EmptyMessages = Array.Empty<LiveMessageEnvelope>();

    public IReadOnlyList<LiveMessageEnvelope> Messages { get; init; } = EmptyMessages;

    public DateTimeOffset? LastReceivedAtUtc { get; init; }

    public long TotalReceived { get; init; }
}
