namespace MeshBoard.Client.Messages;

public sealed record DecryptedMessageSnapshot
{
    public static readonly IReadOnlyList<DecryptedMessageEnvelope> EmptyMessages = Array.Empty<DecryptedMessageEnvelope>();

    public IReadOnlyList<DecryptedMessageEnvelope> Messages { get; init; } = EmptyMessages;

    public DateTimeOffset? LastProjectedAtUtc { get; init; }

    public long TotalProjected { get; init; }
}
