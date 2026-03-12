namespace MeshBoard.Client.Messages;

public sealed record LiveMessageEnvelope
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string PayloadBase64 { get; init; } = string.Empty;

    public int PayloadSizeBytes { get; init; }

    public DateTimeOffset ReceivedAtUtc { get; init; }

    public string Topic { get; init; } = string.Empty;
}
