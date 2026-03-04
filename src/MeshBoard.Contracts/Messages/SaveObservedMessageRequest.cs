namespace MeshBoard.Contracts.Messages;

public sealed class SaveObservedMessageRequest
{
    public required string Topic { get; set; }

    public required string FromNodeId { get; set; }

    public string? ToNodeId { get; set; }

    public required string PayloadPreview { get; set; }

    public bool IsPrivate { get; set; }

    public DateTimeOffset ReceivedAtUtc { get; set; }
}
